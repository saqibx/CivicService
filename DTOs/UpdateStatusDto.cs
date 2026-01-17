using System.ComponentModel.DataAnnotations;
using CivicService.Models;

namespace CivicService.DTOs;

// for updating just the status
public class UpdateStatusDto
{
    [Required]
    [EnumDataType(typeof(ServiceRequestStatus))]
    public ServiceRequestStatus Status { get; set; }
}
