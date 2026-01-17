using CivicService.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicService.Data;

// this is the database context class
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // service requests table
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        
        modelBuilder.Entity<ServiceRequest>(entity =>
        {
            entity.HasKey(e => e.Id);

            // store enums as strings so its easier to read in the db
            entity.Property(e => e.Category).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Address).IsRequired();
        });
    }
}
