using CivicService.Models;

namespace CivicService.DTOs;


// dto for returning service request data
public class ServiceRequestDto
{
    public Guid Id { get; set; }

    public ServiceRequestCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public ServiceRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
