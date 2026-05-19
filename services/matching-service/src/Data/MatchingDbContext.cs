using MatchingService.Domain;
using Microsoft.EntityFrameworkCore;

namespace MatchingService.Data;

public sealed class MatchingDbContext : DbContext
{
    public MatchingDbContext(DbContextOptions<MatchingDbContext> options) : base(options) { }

    public DbSet<PurchaseDealMatch> Matches => Set<PurchaseDealMatch>();
    public DbSet<RecommendationCache> RecommendationCache => Set<RecommendationCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseDealMatch>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.UserId, m.ReceiptId });
            e.Property(m => m.EstimatedSavings).HasPrecision(18, 2);
        });

        modelBuilder.Entity<RecommendationCache>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.DealId).IsUnique();
            e.Property(r => r.DiscountAmount).HasPrecision(18, 2);
        });
    }
}
