using Microsoft.EntityFrameworkCore;
using NotificationService.Domain;

namespace NotificationService.Data;

public sealed class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<UserSubscription> Subscriptions => Set<UserSubscription>();
    public DbSet<NotificationLog> Logs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSubscription>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
            e.Property(s => s.Channel).HasMaxLength(50);
        });

        modelBuilder.Entity<NotificationLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.UserId);
            e.Property(l => l.Message).HasMaxLength(1000);
            e.Property(l => l.Channel).HasMaxLength(50);
        });
    }
}
