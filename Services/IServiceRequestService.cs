using CivicService.DTOs;

namespace CivicService.Services;

public interface IServiceRequestService
{
    Task<ServiceRequestDto> CreateAsync(CreateServiceRequestDto dto, string? userId = null);
    Task<PagedResultDto<ServiceRequestDto>> GetAllAsync(ServiceRequestQueryDto query, string? userId = null, string? ipAddress = null);
    Task<PagedResultDto<ServiceRequestDto>> GetByUserAsync(string userId, ServiceRequestQueryDto query);
    Task<ServiceRequestDto?> GetByIdAsync(Guid id, string? userId = null, string? ipAddress = null);
    Task<ServiceRequestDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto);
    Task<DashboardStatsDto> GetStatisticsAsync();

    // Upvote methods
    Task<bool> UpvoteAsync(Guid requestId, string? userId, string ipAddress);
    Task<bool> RemoveUpvoteAsync(Guid requestId, string? userId, string ipAddress);
}
