using CivicService.Data;
using CivicService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicService.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Add Identity services for the DbContext
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        return context;
    }

    public static async Task<AppDbContext> CreateWithDataAsync()
    {
        var context = Create();

        // Seed test data
        var requests = new List<ServiceRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.Pothole,
                Description = "Large pothole on main street",
                Address = "123 Main St, Downtown, Calgary, AB",
                Neighborhood = "Downtown",
                Latitude = 51.0447,
                Longitude = -114.0719,
                Status = ServiceRequestStatus.Open,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.StreetLight,
                Description = "Street light not working",
                Address = "456 Oak Ave, Beltline, Calgary, AB",
                Neighborhood = "Beltline",
                Status = ServiceRequestStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceRequestCategory.Graffiti,
                Description = "Graffiti on building wall",
                Address = "789 Pine Rd, Downtown, Calgary, AB",
                Neighborhood = "Downtown",
                Status = ServiceRequestStatus.Closed,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };

        context.ServiceRequests.AddRange(requests);
        await context.SaveChangesAsync();

        return context;
    }
}
