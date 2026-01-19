using System.Security.Claims;
using CivicService.DTOs;
using CivicService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicService.Controllers;

[ApiController]
[Route("api/requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly ICaptchaService _captchaService;
    private readonly ILogger<ServiceRequestsController> _logger;

    public ServiceRequestsController(
        IServiceRequestService service,
        ICaptchaService captchaService,
        ILogger<ServiceRequestsController> logger)
    {
        _service = service;
        _captchaService = captchaService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("submissions")]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null && _captchaService.IsConfigured)
        {
            var captchaResult = await VerifyCaptcha(dto.CaptchaToken);
            if (captchaResult != null) return captchaResult;
        }

        var result = await _service.CreateAsync(dto, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    private async Task<IActionResult?> VerifyCaptcha(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { error = "CAPTCHA verification required for anonymous submissions." });
        }

        var isValid = await _captchaService.VerifyAsync(token, "submit_request");
        if (!isValid)
        {
            _logger.LogWarning("CAPTCHA verification failed for IP: {IP}", GetClientIpAddress());
            return BadRequest(new { error = "CAPTCHA verification failed. Please try again." });
        }

        return null;
    }


    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ServiceRequestQueryDto query)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = GetClientIpAddress();
        var results = await _service.GetAllAsync(query, userId, ipAddress);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = GetClientIpAddress();
        var result = await _service.GetByIdAsync(id, userId, ipAddress);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

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


    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _service.GetStatisticsAsync();
        return Ok(stats);
    }

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


    [HttpPost("{id:guid}/upvote")]
    [EnableRateLimiting("upvotes")]
    public async Task<IActionResult> Upvote(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = GetClientIpAddress();

        var success = await _service.UpvoteAsync(id, userId, ipAddress);

        if (!success)
        {
            return Conflict(new { error = "Already upvoted or request not found" });
        }

        return Ok(new { success = true });
    }

    [HttpDelete("{id:guid}/upvote")]
    public async Task<IActionResult> RemoveUpvote(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var ipAddress = GetClientIpAddress();

        var success = await _service.RemoveUpvoteAsync(id, userId, ipAddress);

        if (!success)
        {
            return NotFound(new { error = "Upvote not found" });
        }

        return Ok(new { success = true });
    }

    private string GetClientIpAddress()
    {
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
