using CivicService.Models;

namespace CivicService.DTOs;

// dto for creating a new service request
public class CreateServiceRequestDto
{
    public ServiceRequestCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}
