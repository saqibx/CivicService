using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CivicService.DTOs;
using CivicService.Models;

namespace CivicService.Tests.Integration;

public class ServiceRequestsApiTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServiceRequestsApiTests(WebApplicationFactoryFixture factory)
    {
        _client = factory.CreateClientWithSeedData();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithRequests()
    {
        // Act
        var response = await _client.GetAsync("/api/requests");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<ServiceRequestDto>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_ReturnsFilteredResults()
    {
        // Act
        var response = await _client.GetAsync("/api/requests?status=Open");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<ServiceRequestDto>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.All(result.Items, item => Assert.Equal(ServiceRequestStatus.Open, item.Status));
    }

    [Fact]
    public async Task GetAll_WithPagination_ReturnsCorrectPage()
    {
        // Act
        var response = await _client.GetAsync("/api/requests?page=1&pageSize=1");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<ServiceRequestDto>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsRequest()
    {
        // Arrange
        var validId = "11111111-1111-1111-1111-111111111111";

        // Act
        var response = await _client.GetAsync($"/api/requests/{validId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(Guid.Parse(validId), result.Id);
        Assert.Equal(ServiceRequestCategory.Pothole, result.Category);
    }

    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/requests/{invalidId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        // Arrange
        var dto = new CreateServiceRequestDto
        {
            Category = ServiceRequestCategory.Graffiti,
            Description = "Test graffiti description for integration test",
            Address = "789 Test Ave, TestArea, Calgary, AB",
            Latitude = 51.05,
            Longitude = -114.08
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/requests", dto, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(_jsonOptions);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(ServiceRequestCategory.Graffiti, result.Category);
        Assert.Equal(ServiceRequestStatus.Open, result.Status);
        Assert.Equal("TestArea", result.Neighborhood);
    }

    [Fact]
    public async Task Create_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange - description too short
        var dto = new CreateServiceRequestDto
        {
            Category = ServiceRequestCategory.Pothole,
            Description = "Short", // Less than 10 chars
            Address = "123 Test St"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/requests", dto, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange - missing address
        var dto = new
        {
            Category = "Pothole",
            Description = "Valid description that is long enough"
            // Missing Address
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/requests", dto, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upvote_ValidRequest_ReturnsOk()
    {
        // Arrange
        var requestId = "11111111-1111-1111-1111-111111111111";

        // Act
        var response = await _client.PostAsync($"/api/requests/{requestId}/upvote", null);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Upvote_InvalidRequest_ReturnsConflict()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/requests/{invalidId}/upvote", null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetStats_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/requests/stats");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var requestId = "11111111-1111-1111-1111-111111111111";
        var dto = new UpdateStatusDto { Status = ServiceRequestStatus.InProgress };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/requests/{requestId}/status", dto, _jsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyRequests_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/requests/my");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
