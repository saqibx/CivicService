using CivicService.Models;

namespace CivicService.DTOs;

// query parameters for filtering, sorting, and pagination
public class ServiceRequestQueryDto
{
    public ServiceRequestStatus? Status { get; set; }
    public ServiceRequestCategory? Category { get; set; }
    public string? Sort { get; set; } // e.g., "createdAt_desc", "createdAt_asc"
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
