using Microsoft.EntityFrameworkCore;
using UserService.Domain;

namespace UserService.Data;

public sealed class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.GoogleId).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.GoogleId).HasMaxLength(255);
            e.Property(u => u.Email).HasMaxLength(255);
            e.Property(u => u.DisplayName).HasMaxLength(255);
        });

        modelBuilder.Entity<UserPreference>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.User)
             .WithMany(u => u.Preferences)
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.PreferenceKey).HasMaxLength(100);
        });
    }
}
