using CivicService.Data;
using CivicService.DTOs;
using CivicService.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicService.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ServiceRequestService> _logger;

    public ServiceRequestService(AppDbContext context, ILogger<ServiceRequestService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceRequestDto> CreateAsync(CreateServiceRequestDto dto)
    {
        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = dto.Category,
            Description = dto.Description,
            Address = dto.Address,
            Status = ServiceRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ServiceRequests.Add(serviceRequest);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created service request {Id} - Category: {Category}, Address: {Address}",
            serviceRequest.Id, serviceRequest.Category, serviceRequest.Address);

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

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id)
    {
        var serviceRequest = await _context.ServiceRequests.FindAsync(id);
        return serviceRequest == null ? null : MapToDto(serviceRequest);
    }

    public async Task<ServiceRequestDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto)
    {
        var serviceRequest = await _context.ServiceRequests.FindAsync(id);
        if (serviceRequest == null)
            return null;

        var oldStatus = serviceRequest.Status;
        serviceRequest.Status = dto.Status;
        serviceRequest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated service request {Id} status: {OldStatus} -> {NewStatus}",
            id, oldStatus, dto.Status);

        return MapToDto(serviceRequest);
    }

    private static ServiceRequestDto MapToDto(ServiceRequest request)
    {
        return new ServiceRequestDto
        {
            Id = request.Id,
            Category = request.Category,
            Description = request.Description,
            Address = request.Address,
            Status = request.Status,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt
        };
    }
}
