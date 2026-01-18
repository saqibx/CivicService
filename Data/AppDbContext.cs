using CivicService.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicService.Data;

// Database context with Identity support
public class AppDbContext : IdentityDbContext<ApplicationUser>
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

            // relationship with user (optional - allows guest submissions)
            entity.HasOne(e => e.SubmittedBy)
                .WithMany(u => u.SubmittedRequests)
                .HasForeignKey(e => e.SubmittedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Customize Identity table names (optional, makes them cleaner)
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
        });
    }
}
