# Data Access Guide: EF Core Code-First

## Architecture

Direct `DbContext` usage — no Repository pattern. Code-first approach only.

```
services/user-service/src/
├── Domain/
│   ├── User.cs                 # Entity models
│   ├── UserPreference.cs
│   └── ValueObjects/           # Value objects if needed
├── Data/
│   └── UserDbContext.cs        # DbContext only
├── Features/
│   ├── CreateUser/
│   ├── GetUser/
│   └── UpdatePreferences/
└── Infrastructure/
    └── ServiceCollectionExtensions.cs  # DI setup
```

No Repository interfaces. Services inject `DbContext` directly.

---

## Entity Models (Code-First)

Define entities first. EF generates schema from these.

### User Service

```csharp
// Domain/User.cs
namespace ExpenseTracker.UserService.Domain;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string OAuthId { get; set; } = null!;      // Google OAuth sub claim
    public string OAuthProvider { get; set; } = null!; // "google"
    public string? PreferredLocation { get; set; }     // Postal code
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserPreference> Preferences { get; set; } = [];
}

// Domain/UserPreference.cs
namespace ExpenseTracker.UserService.Domain;

public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Key { get; set; } = null!;   // e.g., "email_frequency"
    public string Value { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
```

### Receipt Service

```csharp
// Domain/Receipt.cs
namespace ExpenseTracker.ReceiptService.Domain;

public class Receipt
{
    public int Id { get; set; }
    public int UserId { get; set; }             // From X-User-Id header (no FK to User Service)
    public string Store { get; set; } = null!;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ImagePath { get; set; }       // Path to stored receipt image
    public DateTime CreatedAt { get; set; }

    public ICollection<ReceiptItem> Items { get; set; } = [];
}

// Domain/ReceiptItem.cs
namespace ExpenseTracker.ReceiptService.Domain;

public class ReceiptItem
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public string ProductName { get; set; } = null!;
    public string Category { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string Unit { get; set; } = "piece";

    public Receipt Receipt { get; set; } = null!;
}
```

### Deal Service

```csharp
// Domain/Deal.cs
namespace ExpenseTracker.DealService.Domain;

public class Deal
{
    public int Id { get; set; }
    public string ProductName { get; set; } = null!;
    public string Retailer { get; set; } = null!;   // Aldi, Lidl, Rewe, Edeka
    public decimal RegularPrice { get; set; }
    public decimal DealPrice { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? LocationZip { get; set; }         // null = national/online deal
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = null!;     // "manual", "scraper", "api"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public decimal DiscountPercent => RegularPrice > 0
        ? (RegularPrice - DealPrice) / RegularPrice * 100
        : 0;

    [NotMapped]
    public bool IsActive => DateTime.UtcNow >= ValidFrom && DateTime.UtcNow <= ValidTo;

    [NotMapped]
    public bool IsNational => LocationZip is null;
}
```

---

## DbContext Configuration

One `DbContext` per service, configured with code-first conventions.

```csharp
// Data/UserDbContext.cs
namespace ExpenseTracker.UserService.Data;

using Microsoft.EntityFrameworkCore;
using ExpenseTracker.UserService.Domain;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreference> Preferences => Set<UserPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.OAuthId)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.OAuthProvider)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => new { e.OAuthProvider, e.OAuthId })
                .IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(e => e.Preferences)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Value)
                .IsRequired();

            entity.HasIndex(e => new { e.UserId, e.Key })
                .IsUnique();
        });
    }
}

// Data/ReceiptDbContext.cs
namespace ExpenseTracker.ReceiptService.Data;

using Microsoft.EntityFrameworkCore;
using ExpenseTracker.ReceiptService.Domain;

public class ReceiptDbContext(DbContextOptions<ReceiptDbContext> options) : DbContext(options)
{
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> Items => Set<ReceiptItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Receipt>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TotalAmount)
                .HasPrecision(10, 2);

            entity.HasIndex(e => e.UserId);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Receipt)
                .HasForeignKey(i => i.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReceiptItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Price)
                .HasPrecision(10, 2);

            entity.Property(e => e.Quantity)
                .HasPrecision(8, 3);

            entity.HasIndex(e => new { e.ReceiptId, e.ProductName });
        });
    }
}

// Data/DealDbContext.cs
namespace ExpenseTracker.DealService.Data;

using Microsoft.EntityFrameworkCore;
using ExpenseTracker.DealService.Domain;

public class DealDbContext(DbContextOptions<DealDbContext> options) : DbContext(options)
{
    public DbSet<Deal> Deals => Set<Deal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Deal>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RegularPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.DealPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.LocationZip)
                .HasMaxLength(10);   // nullable — null = national/online deal

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.Retailer, e.ValidFrom, e.ValidTo });
            entity.HasIndex(e => e.LocationZip);  // partial index on non-null values
            entity.HasIndex(e => e.ProductName);
        });
    }
}
```

---

## Dependency Injection Setup

```csharp
// Infrastructure/ServiceCollectionExtensions.cs
namespace ExpenseTracker.UserService.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExpenseTracker.UserService.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<UserDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                npgsqlOptions.MigrationsAssembly(typeof(UserDbContext).Assembly.GetName().Name);
            }));

        return services;
    }
}

// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataAccess(builder.Configuration);

var app = builder.Build();
app.Run();
```

---

## Code-First Migrations

All schema changes via migrations. Never edit the database directly.

### Initial Migration

```bash
cd services/user-service

dotnet ef migrations add InitialCreate \
  --startup-project . \
  --output-dir src/Data/Migrations

# Review the generated file before applying
dotnet ef database update

dotnet ef migrations list
```

### Add a New Entity

```bash
# 1. Create entity in Domain/
# 2. Add DbSet to DbContext
# 3. Configure in OnModelCreating
# 4. Create migration
dotnet ef migrations add AddNewEntity

# 5. Review migration file
# 6. Apply
dotnet ef database update
```

---

## Using DbContext in Services

No repositories. Inject `DbContext` directly. Use primary constructors.

```csharp
// Features/CreateUser/CreateUserHandler.cs
namespace ExpenseTracker.UserService.Features.CreateUser;

using ExpenseTracker.UserService.Data;
using ExpenseTracker.UserService.Domain;

public record CreateUserCommand(string Email, string Name, string OAuthId, string OAuthProvider);
public record CreateUserResult(int UserId, string Email);

public class CreateUserHandler(UserDbContext dbContext, ILogger<CreateUserHandler> logger)
{
    public async Task<CreateUserResult> HandleAsync(CreateUserCommand cmd)
    {
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.OAuthProvider == cmd.OAuthProvider
                                   && u.OAuthId == cmd.OAuthId);

        if (existingUser != null)
            return new CreateUserResult(existingUser.Id, existingUser.Email);

        var user = new User
        {
            Email = cmd.Email,
            Name = cmd.Name,
            OAuthId = cmd.OAuthId,
            OAuthProvider = cmd.OAuthProvider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created user {UserId} with email {Email}", user.Id, user.Email);

        return new CreateUserResult(user.Id, user.Email);
    }
}
```

---

## Connection String Configuration

### Local Development (appsettings.Development.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-users;Port=5432;Database=users_db;Username=postgres;Password=localpassword;"
  }
}
```

### Production (environment variables)

```bash
DATABASE_URL=Host=postgres-users;Port=5432;Database=users_db;Username=postgres;Password=${SECURE_PASSWORD};
```

### Read from environment in code

```csharp
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? configuration.GetConnectionString("DefaultConnection");
```

---

## Migration Management

```bash
# Generate migration without applying
dotnet ef migrations add SomeChange

# List all migrations
dotnet ef migrations list

# Remove pending (not yet applied) migration
dotnet ef migrations remove

# Revert to specific migration
dotnet ef database update NameOfMigration

# Generate SQL script to review
dotnet ef migrations script > migration.sql
```

---

## Best Practices

1. **Always review migrations before applying** — check the `.cs` file in `Migrations/`
2. **Migrations are versioned code** — commit them to git, never edit generated migrations manually
3. **Use descriptive migration names** — `AddPhoneNumberToUser`, not `Update`
4. **Configure in OnModelCreating** — indexes, constraints, precision, relationships
5. **No lazy loading in distributed systems** — always `.Include()` related data explicitly
6. **No cross-service foreign keys** — services reference each other by ID only, not via EF navigation

---

## Troubleshooting

### Reset local database

```bash
dotnet ef database update 0
# Delete migration files from src/Data/Migrations/
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Migration conflicts (after branch merge)

```bash
# Keep both migrations, then re-add to update the snapshot
dotnet ef migrations add RebaseAfterMerge
```
