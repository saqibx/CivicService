using CivicService.Models;

namespace CivicService.DTOs;

// for updating just the status
public class UpdateStatusDto
{
    public ServiceRequestStatus Status { get; set; }
}
