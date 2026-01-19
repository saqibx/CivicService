using CivicService.Data;
using CivicService.DTOs;
using CivicService.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicService.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ServiceRequestService> _logger;
    private readonly IEmailService _emailService;

    public ServiceRequestService(AppDbContext context, ILogger<ServiceRequestService> logger, IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<ServiceRequestDto> CreateAsync(CreateServiceRequestDto dto, string? userId = null)
    {
        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = dto.Category,
            Description = dto.Description,
            Address = dto.Address,
            Neighborhood = ExtractNeighborhood(dto.Address),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Status = ServiceRequestStatus.Open,
            SubmittedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created service request {Id} - Category: {Category}, Address: {Address}, UserId: {UserId}",
            serviceRequest.Id, serviceRequest.Category, serviceRequest.Address, userId ?? "guest");

        return MapToDto(serviceRequest, userId, null);
    }

    public async Task<PagedResultDto<ServiceRequestDto>> GetAllAsync(ServiceRequestQueryDto query, string? userId = null, string? ipAddress = null)
    {
        var queryable = _context.ServiceRequests
            .Include(r => r.Upvotes)
            .AsQueryable();

        queryable = ApplyFilters(queryable, query);

        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.Sort);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ServiceRequestDto>
        {
            Items = items.Select(r => MapToDto(r, userId, ipAddress)),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private IQueryable<ServiceRequest> ApplyFilters(IQueryable<ServiceRequest> queryable, ServiceRequestQueryDto query)
    {
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(r => r.Status == query.Status.Value);
        }

        if (query.Category.HasValue)
        {
            queryable = queryable.Where(r => r.Category == query.Category.Value);
        }

        return queryable;
    }

    private IQueryable<ServiceRequest> ApplySorting(IQueryable<ServiceRequest> queryable, string? sort)
    {
        return sort?.ToLower() switch
        {
            "createdat_asc" => queryable.OrderBy(r => r.CreatedAt),
            "createdat_desc" => queryable.OrderByDescending(r => r.CreatedAt),
            "updatedat_asc" => queryable.OrderBy(r => r.UpdatedAt),
            "updatedat_desc" => queryable.OrderByDescending(r => r.UpdatedAt),
            "upvotes_desc" => queryable.OrderByDescending(r => r.Upvotes.Count).ThenByDescending(r => r.CreatedAt),
            _ => queryable.OrderByDescending(r => r.CreatedAt)
        };
    }

    public async Task<PagedResultDto<ServiceRequestDto>> GetByUserAsync(string userId, ServiceRequestQueryDto query)
    {
        var queryable = _context.ServiceRequests
            .Include(r => r.Upvotes)
            .Where(r => r.SubmittedById == userId);

        queryable = ApplyFilters(queryable, query);

        var totalCount = await queryable.CountAsync();

        queryable = ApplySorting(queryable, query.Sort);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ServiceRequestDto>
        {
            Items = items.Select(r => MapToDto(r, userId, null)),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id, string? userId = null, string? ipAddress = null)
    {
        var serviceRequest = await _context.ServiceRequests
            .Include(r => r.Upvotes)
            .FirstOrDefaultAsync(r => r.Id == id);
        return serviceRequest == null ? null : MapToDto(serviceRequest, userId, ipAddress);
    }

    public async Task<ServiceRequestDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto)
    {
        var serviceRequest = await _context.ServiceRequests
            .Include(r => r.SubmittedBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (serviceRequest == null)
            return null;

        var oldStatus = serviceRequest.Status;
        serviceRequest.Status = dto.Status;
        serviceRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated service request {Id} status: {OldStatus} -> {NewStatus}",
            id, oldStatus, dto.Status);

        await SendStatusEmailIfNeeded(serviceRequest, oldStatus, dto.Status);

        return MapToDto(serviceRequest, null, null);
    }

    private async Task SendStatusEmailIfNeeded(ServiceRequest request, ServiceRequestStatus oldStatus, ServiceRequestStatus newStatus)
    {
        if (request.SubmittedBy != null && !string.IsNullOrEmpty(request.SubmittedBy.Email))
        {
            var userName = $"{request.SubmittedBy.FirstName} {request.SubmittedBy.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(userName))
                userName = request.SubmittedBy.Email;

            await _emailService.SendStatusUpdateEmailAsync(
                request.SubmittedBy.Email,
                userName,
                request.Id.ToString(),
                request.Category.ToString(),
                oldStatus.ToString(),
                newStatus.ToString()
            );
        }
    }

    public async Task<bool> UpvoteAsync(Guid requestId, string? userId, string ipAddress)
    {
        var request = await _context.ServiceRequests.FindAsync(requestId);
        if (request == null)
            return false;

        var existingUpvote = await _context.Upvotes
            .FirstOrDefaultAsync(u => u.ServiceRequestId == requestId &&
                ((userId != null && u.UserId == userId) || (userId == null && u.IpAddress == ipAddress)));

        if (existingUpvote != null)
            return false;

        var upvote = new Upvote
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            UserId = userId,
            IpAddress = userId == null ? ipAddress : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Upvotes.Add(upvote);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Upvote added to request {RequestId} by {UserOrIp}",
            requestId, userId ?? $"IP:{ipAddress}");

        return true;
    }

    public async Task<bool> RemoveUpvoteAsync(Guid requestId, string? userId, string ipAddress)
    {
        var upvote = await _context.Upvotes
            .FirstOrDefaultAsync(u => u.ServiceRequestId == requestId &&
                ((userId != null && u.UserId == userId) || (userId == null && u.IpAddress == ipAddress)));

        if (upvote == null)
            return false;

        _context.Upvotes.Remove(upvote);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Upvote removed from request {RequestId} by {UserOrIp}",
            requestId, userId ?? $"IP:{ipAddress}");

        return true;
    }

    public async Task<DashboardStatsDto> GetStatisticsAsync()
    {
        var requests = await _context.ServiceRequests.ToListAsync();

        var byStatus = requests
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var byCategory = requests
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        var overTime = GetRequestsOverTime(requests);
        var avgResolutionHours = CalculateAvgResolutionTime(requests);
        var topNeighborhoods = GetTopNeighborhoods(requests);

        return new DashboardStatsDto
        {
            TotalRequests = requests.Count,
            ByStatus = byStatus,
            ByCategory = byCategory,
            RequestsOverTime = overTime,
            AverageResolutionHours = avgResolutionHours,
            TopNeighborhoods = topNeighborhoods
        };
    }

    private List<DailyCountDto> GetRequestsOverTime(List<ServiceRequest> requests)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        return requests
            .Where(r => r.CreatedAt >= thirtyDaysAgo)
            .GroupBy(r => r.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyCountDto { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
            .ToList();
    }

    private double CalculateAvgResolutionTime(List<ServiceRequest> requests)
    {
        var closedRequests = requests.Where(r => r.Status == ServiceRequestStatus.Closed).ToList();
        if (closedRequests.Count == 0)
            return 0;

        var avgHours = closedRequests.Average(r => (r.UpdatedAt - r.CreatedAt).TotalHours);
        return Math.Round(avgHours, 1);
    }

    private List<NeighborhoodCountDto> GetTopNeighborhoods(List<ServiceRequest> requests)
    {
        return requests
            .GroupBy(r => r.Neighborhood ?? ExtractNeighborhood(r.Address))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new NeighborhoodCountDto { Neighborhood = g.Key, Count = g.Count() })
            .ToList();
    }


    private static ServiceRequestDto MapToDto(ServiceRequest request, string? currentUserId, string? currentIpAddress)
    {
        var hasUpvoted = false;
        if (request.Upvotes != null)
        {
            hasUpvoted = request.Upvotes.Any(u =>
                (currentUserId != null && u.UserId == currentUserId) ||
                (currentUserId == null && currentIpAddress != null && u.IpAddress == currentIpAddress));
        }

        return new ServiceRequestDto
        {
            Id = request.Id,
            Category = request.Category,
            Description = request.Description,
            Address = request.Address,
            Neighborhood = request.Neighborhood,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt,
            SubmittedById = request.SubmittedById,
            UpvoteCount = request.Upvotes?.Count ?? 0,
            HasUpvoted = hasUpvoted
        };
    }


    private static string ExtractNeighborhood(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Unknown";

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
        {
            return parts[1];
        }

        return parts.Length > 0 ? parts[0] : "Unknown";
    }
}
