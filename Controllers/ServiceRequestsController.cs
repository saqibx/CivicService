using CivicService.DTOs;
using CivicService.Services;
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ServiceRequestQueryDto query)
    {
        var results = await _service.GetAllAsync(query);
        return Ok(results);
    }

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

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _service.GetStatisticsAsync();
        return Ok(stats);
    }
}
