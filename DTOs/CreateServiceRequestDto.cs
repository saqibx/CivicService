using System.ComponentModel.DataAnnotations;
using CivicService.Models;

namespace CivicService.DTOs;

// dto for creating a new service request
public class CreateServiceRequestDto
{
    [Required]
    [EnumDataType(typeof(ServiceRequestCategory))]
    public ServiceRequestCategory Category { get; set; }

    [Required]
    [MinLength(10, ErrorMessage = "Description must be at least 10 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;
}
