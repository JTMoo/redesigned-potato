using Microsoft.EntityFrameworkCore;
using ReceiptService.Domain;

namespace ReceiptService.Data;

public sealed class ReceiptDbContext : DbContext
{
    public ReceiptDbContext(DbContextOptions<ReceiptDbContext> options) : base(options) { }

    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Receipt>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.StoreName).HasMaxLength(255);
            e.Property(r => r.TotalAmount).HasPrecision(18, 2);
            e.Property(r => r.Status).HasConversion<string>();
        });

        modelBuilder.Entity<ReceiptItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasOne(i => i.Receipt)
             .WithMany(r => r.Items)
             .HasForeignKey(i => i.ReceiptId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.Description).HasMaxLength(500);
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.Property(i => i.Total).HasPrecision(18, 2);
        });
    }
}
