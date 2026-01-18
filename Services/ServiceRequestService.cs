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

        return MapToDto(serviceRequest);
    }

    public async Task<PagedResultDto<ServiceRequestDto>> GetAllAsync(ServiceRequestQueryDto query)
    {
        var queryable = _context.ServiceRequests.AsQueryable();

        // filtering
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(r => r.Status == query.Status.Value);
        }

        if (query.Category.HasValue)
        {
            queryable = queryable.Where(r => r.Category == query.Category.Value);
        }

        // get total count before pagination
        var totalCount = await queryable.CountAsync();

        // sorting
        queryable = query.Sort?.ToLower() switch
        {
            "createdat_asc" => queryable.OrderBy(r => r.CreatedAt),
            "createdat_desc" => queryable.OrderByDescending(r => r.CreatedAt),
            "updatedat_asc" => queryable.OrderBy(r => r.UpdatedAt),
            "updatedat_desc" => queryable.OrderByDescending(r => r.UpdatedAt),
            _ => queryable.OrderByDescending(r => r.CreatedAt) // default
        };

        // pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ServiceRequestDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResultDto<ServiceRequestDto>> GetByUserAsync(string userId, ServiceRequestQueryDto query)
    {
        var queryable = _context.ServiceRequests
            .Where(r => r.SubmittedById == userId);

        // filtering
        if (query.Status.HasValue)
        {
            queryable = queryable.Where(r => r.Status == query.Status.Value);
        }

        if (query.Category.HasValue)
        {
            queryable = queryable.Where(r => r.Category == query.Category.Value);
        }

        // get total count before pagination
        var totalCount = await queryable.CountAsync();

        // sorting
        queryable = query.Sort?.ToLower() switch
        {
            "createdat_asc" => queryable.OrderBy(r => r.CreatedAt),
            "createdat_desc" => queryable.OrderByDescending(r => r.CreatedAt),
            "updatedat_asc" => queryable.OrderBy(r => r.UpdatedAt),
            "updatedat_desc" => queryable.OrderByDescending(r => r.UpdatedAt),
            _ => queryable.OrderByDescending(r => r.CreatedAt)
        };

        // pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResultDto<ServiceRequestDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id)
    {
        var serviceRequest = await _context.ServiceRequests.FindAsync(id);
        return serviceRequest == null ? null : MapToDto(serviceRequest);
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

        // Send email notification if request was submitted by a logged-in user (not anonymous)
        if (serviceRequest.SubmittedBy != null && !string.IsNullOrEmpty(serviceRequest.SubmittedBy.Email))
        {
            var userName = $"{serviceRequest.SubmittedBy.FirstName} {serviceRequest.SubmittedBy.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(userName))
                userName = serviceRequest.SubmittedBy.Email;

            await _emailService.SendStatusUpdateEmailAsync(
                serviceRequest.SubmittedBy.Email,
                userName,
                serviceRequest.Id.ToString(),
                serviceRequest.Category.ToString(),
                oldStatus.ToString(),
                dto.Status.ToString()
            );
        }

        return MapToDto(serviceRequest);
    }

    public async Task<DashboardStatsDto> GetStatisticsAsync()
    {
        var requests = await _context.ServiceRequests.ToListAsync();

        // counts by status
        var byStatus = requests
            .GroupBy(r => r.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // counts by category
        var byCategory = requests
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // requests over time (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var overTime = requests
            .Where(r => r.CreatedAt >= thirtyDaysAgo)
            .GroupBy(r => r.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyCountDto { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() })
            .ToList();

        // average resolution time (for closed requests)
        var closedRequests = requests.Where(r => r.Status == ServiceRequestStatus.Closed).ToList();
        double avgResolutionHours = 0;
        if (closedRequests.Count > 0)
        {
            avgResolutionHours = closedRequests
                .Average(r => (r.UpdatedAt - r.CreatedAt).TotalHours);
        }

        // top neighborhoods (group by neighborhood, falling back to address extraction for older records)
        var topNeighborhoods = requests
            .GroupBy(r => r.Neighborhood ?? ExtractNeighborhood(r.Address))
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new NeighborhoodCountDto { Neighborhood = g.Key, Count = g.Count() })
            .ToList();

        return new DashboardStatsDto
        {
            TotalRequests = requests.Count,
            ByStatus = byStatus,
            ByCategory = byCategory,
            RequestsOverTime = overTime,
            AverageResolutionHours = Math.Round(avgResolutionHours, 1),
            TopNeighborhoods = topNeighborhoods
        };
    }

    private static ServiceRequestDto MapToDto(ServiceRequest request)
    {
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
            SubmittedById = request.SubmittedById
        };
    }

    /// <summary>
    /// Extracts neighborhood/locality from a formatted address.
    /// Google Places addresses typically follow: "Street, Neighborhood/City, State ZIP, Country"
    /// This method extracts the second component which is usually the neighborhood or city.
    /// </summary>
    private static string ExtractNeighborhood(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Unknown";

        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // If we have at least 2 parts, the second one is typically the neighborhood/locality
        if (parts.Length >= 2)
        {
            return parts[1];
        }

        // If only one part, return it as the neighborhood
        return parts.Length > 0 ? parts[0] : "Unknown";
    }
}
