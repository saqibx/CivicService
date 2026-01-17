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

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }
}
