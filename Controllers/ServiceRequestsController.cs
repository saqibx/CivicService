using System.Security.Claims;
using CivicService.DTOs;
using CivicService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicService.Controllers;

[ApiController]
[Route("api/requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly ILogger<ServiceRequestsController> _logger;

    public ServiceRequestsController(IServiceRequestService service, ILogger<ServiceRequestsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Create a new service request (allows both guests and authenticated users)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
    {
        // Get user ID if authenticated, otherwise null (guest submission)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var result = await _service.CreateAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get all service requests with filtering and pagination (public)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ServiceRequestQueryDto query)
    {
        var results = await _service.GetAllAsync(query);
        return Ok(results);
    }

    /// <summary>
    /// Get a specific service request by ID (public)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Update the status of a service request (Admin only)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Get dashboard statistics (Admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _service.GetStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Get requests submitted by the current user (authenticated users only)
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyRequests([FromQuery] ServiceRequestQueryDto query)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var results = await _service.GetByUserAsync(userId, query);
        return Ok(results);
    }
}
