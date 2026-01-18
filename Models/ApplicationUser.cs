using Microsoft.AspNetCore.Identity;

namespace CivicService.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property for submitted requests
    public ICollection<ServiceRequest> SubmittedRequests { get; set; } = [];
}

// Role constants for the application
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Citizen = "Citizen";

    public static readonly string[] All = [Admin, Staff, Citizen];
}
