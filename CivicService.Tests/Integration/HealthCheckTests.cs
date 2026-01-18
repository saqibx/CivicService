using System.Net;
using System.Net.Http.Json;

namespace CivicService.Tests.Integration;

public class HealthCheckTests : IClassFixture<WebApplicationFactoryFixture>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactoryFixture factory)
    {
        _client = factory.CreateClientWithSeedData();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content.Status);
        Assert.NotNull(content.Checks);
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record HealthResponse(string Status, DateTime Timestamp, double Duration, HealthCheck[] Checks);
    private record HealthCheck(string Name, string Status, double Duration, string? Description, string? Exception);
}
