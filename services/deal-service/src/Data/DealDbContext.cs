using DealService.Domain;
using Microsoft.EntityFrameworkCore;

namespace DealService.Data;

public sealed class DealDbContext : DbContext
{
    public DealDbContext(DbContextOptions<DealDbContext> options) : base(options) { }

    public DbSet<Deal> Deals => Set<Deal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Deal>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Title).HasMaxLength(255);
            e.Property(d => d.Description).HasMaxLength(2000);
            e.Property(d => d.DiscountAmount).HasPrecision(18, 2);
            e.Property(d => d.LocationZip).HasMaxLength(10);
            e.HasIndex(d => d.IsActive);
        });
    }
}
