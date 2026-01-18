namespace CivicService.Models;


// main model for service requests
public class ServiceRequest
{
    public Guid Id { get; set; }

    public ServiceRequestCategory Category { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    // neighborhood/locality extracted from address for grouping
    public string? Neighborhood { get; set; }

    // geographic coordinates for map display
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ServiceRequestStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // User who submitted the request (null for guest submissions)
    public string? SubmittedById { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }

    // Upvotes from users affected by the same issue
    public ICollection<Upvote> Upvotes { get; set; } = [];
}
