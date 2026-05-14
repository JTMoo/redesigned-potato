using AggregationService.Domain;
using Microsoft.EntityFrameworkCore;

namespace AggregationService.Data;

public sealed class AggregationDbContext : DbContext
{
    public AggregationDbContext(DbContextOptions<AggregationDbContext> options) : base(options) { }

    public DbSet<DealSource> DealSources => Set<DealSource>();
    public DbSet<ScrapeJob> ScrapeJobs => Set<ScrapeJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DealSource>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(255);
            e.Property(s => s.BaseUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<ScrapeJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasOne(j => j.DealSource)
             .WithMany()
             .HasForeignKey(j => j.DealSourceId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(j => j.Status).HasMaxLength(50);
        });
    }
}
