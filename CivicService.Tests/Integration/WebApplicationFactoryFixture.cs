using CivicService.Data;
using CivicService.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicService.Tests.Integration;

public class WebApplicationFactoryFixture : WebApplicationFactory<Program>
{
    private bool _dataSeeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core service registrations to avoid provider conflicts
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing with a unique name per factory instance
            var dbName = "TestDb_" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });

        builder.UseEnvironment("Development");
    }

    public HttpClient CreateClientWithSeedData()
    {
        var client = CreateClient();

        if (!_dataSeeded)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            SeedTestData(db);
            _dataSeeded = true;
        }

        return client;
    }

    private static void SeedTestData(AppDbContext context)
    {
        if (context.ServiceRequests.Any())
            return;

        var requests = new List<ServiceRequest>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Category = ServiceRequestCategory.Pothole,
                Description = "Large pothole on main street causing damage",
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
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Category = ServiceRequestCategory.StreetLight,
                Description = "Street light flickering at night",
                Address = "456 Oak Ave, Beltline, Calgary, AB",
                Neighborhood = "Beltline",
                Status = ServiceRequestStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        context.ServiceRequests.AddRange(requests);
        context.SaveChanges();
    }
}
