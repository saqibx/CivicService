namespace CivicService.Models;

public class Upvote
{
    public Guid Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    // For logged-in users
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // For anonymous users (to prevent duplicate votes)
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
