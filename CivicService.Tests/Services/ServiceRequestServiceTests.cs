using CivicService.DTOs;
using CivicService.Models;
using CivicService.Services;
using CivicService.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace CivicService.Tests.Services;

public class ServiceRequestServiceTests
{
    private readonly Mock<ILogger<ServiceRequestService>> _loggerMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    public ServiceRequestServiceTests()
    {
        _loggerMock = new Mock<ILogger<ServiceRequestService>>();
        _emailServiceMock = new Mock<IEmailService>();
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesRequest()
    {
        // Arrange
        var context = TestDbContextFactory.Create();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var dto = new CreateServiceRequestDto
        {
            Category = ServiceRequestCategory.Pothole,
            Description = "Test pothole description",
            Address = "123 Test St, TestNeighborhood, Calgary, AB",
            Latitude = 51.0447,
            Longitude = -114.0719
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(ServiceRequestCategory.Pothole, result.Category);
        Assert.Equal("Test pothole description", result.Description);
        Assert.Equal(ServiceRequestStatus.Open, result.Status);
        Assert.Equal("TestNeighborhood", result.Neighborhood);
    }

    [Fact]
    public async Task CreateAsync_WithUserId_AssociatesUser()
    {
        // Arrange
        var context = TestDbContextFactory.Create();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var userId = "test-user-123";
        var dto = new CreateServiceRequestDto
        {
            Category = ServiceRequestCategory.Graffiti,
            Description = "Graffiti on the wall",
            Address = "456 Main St, Downtown, Calgary"
        };

        // Act
        var result = await service.CreateAsync(dto, userId);

        // Assert
        Assert.Equal(userId, result.SubmittedById);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResults()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var query = new ServiceRequestQueryDto { Page = 1, PageSize = 10 };

        // Act
        var result = await service.GetAllAsync(query);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.Items.Count());
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_FiltersCorrectly()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var query = new ServiceRequestQueryDto
        {
            Status = ServiceRequestStatus.Open,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetAllAsync(query);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(ServiceRequestStatus.Open, item.Status));
    }

    [Fact]
    public async Task GetAllAsync_WithCategoryFilter_FiltersCorrectly()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var query = new ServiceRequestQueryDto
        {
            Category = ServiceRequestCategory.Pothole,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetAllAsync(query);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(ServiceRequestCategory.Pothole, item.Category));
    }

    [Fact]
    public async Task GetAllAsync_WithSorting_SortsCorrectly()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var query = new ServiceRequestQueryDto
        {
            Sort = "createdAt_asc",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetAllAsync(query);
        var items = result.Items.ToList();

        // Assert
        Assert.True(items[0].CreatedAt <= items[1].CreatedAt);
        Assert.True(items[1].CreatedAt <= items[2].CreatedAt);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsRequest()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var existingRequest = context.ServiceRequests.First();

        // Act
        var result = await service.GetByIdAsync(existingRequest.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingRequest.Id, result.Id);
        Assert.Equal(existingRequest.Description, result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithValidId_UpdatesStatus()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var existingRequest = context.ServiceRequests.First(r => r.Status == ServiceRequestStatus.Open);
        var dto = new UpdateStatusDto { Status = ServiceRequestStatus.InProgress };

        // Act
        var result = await service.UpdateStatusAsync(existingRequest.Id, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ServiceRequestStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var dto = new UpdateStatusDto { Status = ServiceRequestStatus.Closed };

        // Act
        var result = await service.UpdateStatusAsync(Guid.NewGuid(), dto);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);

        // Act
        var stats = await service.GetStatisticsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRequests);
        Assert.True(stats.ByStatus.ContainsKey("Open"));
        Assert.True(stats.ByStatus.ContainsKey("InProgress"));
        Assert.True(stats.ByStatus.ContainsKey("Closed"));
        Assert.Equal(1, stats.ByStatus["Open"]);
        Assert.Equal(1, stats.ByStatus["InProgress"]);
        Assert.Equal(1, stats.ByStatus["Closed"]);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsTopNeighborhoods()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);

        // Act
        var stats = await service.GetStatisticsAsync();

        // Assert
        Assert.NotEmpty(stats.TopNeighborhoods);
        // Downtown has 2 requests, should be first
        Assert.Equal("Downtown", stats.TopNeighborhoods.First().Neighborhood);
        Assert.Equal(2, stats.TopNeighborhoods.First().Count);
    }

    [Fact]
    public async Task UpvoteAsync_FirstUpvote_ReturnsTrue()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var requestId = context.ServiceRequests.First().Id;

        // Act
        var result = await service.UpvoteAsync(requestId, "user-123", "192.168.1.1");

        // Assert
        Assert.True(result);
        Assert.Equal(1, context.Upvotes.Count(u => u.ServiceRequestId == requestId));
    }

    [Fact]
    public async Task UpvoteAsync_DuplicateUpvote_ReturnsFalse()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var requestId = context.ServiceRequests.First().Id;

        // Act
        await service.UpvoteAsync(requestId, "user-123", "192.168.1.1");
        var secondResult = await service.UpvoteAsync(requestId, "user-123", "192.168.1.1");

        // Assert
        Assert.False(secondResult);
        Assert.Equal(1, context.Upvotes.Count(u => u.ServiceRequestId == requestId));
    }

    [Fact]
    public async Task UpvoteAsync_AnonymousUpvote_TracksIpAddress()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var requestId = context.ServiceRequests.First().Id;

        // Act
        var result = await service.UpvoteAsync(requestId, null, "192.168.1.100");

        // Assert
        Assert.True(result);
        var upvote = context.Upvotes.First(u => u.ServiceRequestId == requestId);
        Assert.Null(upvote.UserId);
        Assert.Equal("192.168.1.100", upvote.IpAddress);
    }

    [Fact]
    public async Task RemoveUpvoteAsync_ExistingUpvote_ReturnsTrue()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var requestId = context.ServiceRequests.First().Id;
        await service.UpvoteAsync(requestId, "user-123", "192.168.1.1");

        // Act
        var result = await service.RemoveUpvoteAsync(requestId, "user-123", "192.168.1.1");

        // Assert
        Assert.True(result);
        Assert.Equal(0, context.Upvotes.Count(u => u.ServiceRequestId == requestId));
    }

    [Fact]
    public async Task RemoveUpvoteAsync_NonExistingUpvote_ReturnsFalse()
    {
        // Arrange
        var context = await TestDbContextFactory.CreateWithDataAsync();
        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var requestId = context.ServiceRequests.First().Id;

        // Act
        var result = await service.RemoveUpvoteAsync(requestId, "user-123", "192.168.1.1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsOnlyUserRequests()
    {
        // Arrange
        var context = TestDbContextFactory.Create();
        var userId = "test-user-456";

        // Add requests for different users
        context.ServiceRequests.AddRange(
            new ServiceRequest
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.Pothole,
                Description = "User's request",
                Address = "123 User St",
                SubmittedById = userId,
                Status = ServiceRequestStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ServiceRequest
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.Graffiti,
                Description = "Another user's request",
                Address = "456 Other St",
                SubmittedById = "other-user",
                Status = ServiceRequestStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ServiceRequest
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.StreetLight,
                Description = "Guest request",
                Address = "789 Guest St",
                SubmittedById = null,
                Status = ServiceRequestStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        var service = new ServiceRequestService(context, _loggerMock.Object, _emailServiceMock.Object);
        var query = new ServiceRequestQueryDto { Page = 1, PageSize = 10 };

        // Act
        var result = await service.GetByUserAsync(userId, query);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(userId, item.SubmittedById));
    }
}
