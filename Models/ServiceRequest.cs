namespace CivicService.Models;


// main model for service requests
public class ServiceRequest
{
    public Guid Id { get; set; }

    public ServiceRequestCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty; // using address instead of lat/long for now

    public ServiceRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
