using CivicService.DTOs;

namespace CivicService.Services;

public interface IServiceRequestService
{
    Task<ServiceRequestDto> CreateAsync(CreateServiceRequestDto dto);
    Task<PagedResultDto<ServiceRequestDto>> GetAllAsync(ServiceRequestQueryDto query);
    Task<ServiceRequestDto?> GetByIdAsync(Guid id);
    Task<ServiceRequestDto?> UpdateStatusAsync(Guid id, UpdateStatusDto dto);
}
