# Data Access Guide: EF Core Code-First

## Architecture

Direct `DbContext` usage - no Repository pattern. Code-first approach only.

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

### User Service Example

```csharp
// Domain/User.cs
namespace ExpenseTracker.UserService.Domain;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string OAuthId { get; set; } = null!;  // OAuth provider ID
    public string? PreferredLocation { get; set; }  // Postal code
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<UserPreference> Preferences { get; set; } = [];
}

// Domain/UserPreference.cs
namespace ExpenseTracker.UserService.Domain;

public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Key { get; set; } = null!;  // e.g., "email_frequency"
    public string Value { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }

    // Foreign key
    public User User { get; set; } = null!;
}
```

### Receipt Service Example

```csharp
// Domain/Receipt.cs
namespace ExpenseTracker.ReceiptService.Domain;

public class Receipt
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Store { get; set; } = null!;
    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ImagePath { get; set; }  // Path to stored receipt image
    public DateTime CreatedAt { get; set; }

    // Navigation property
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

    // Foreign key
    public Receipt Receipt { get; set; } = null!;
}
```

### Deal Service Example

```csharp
// Domain/Deal.cs
namespace ExpenseTracker.DealService.Domain;

public class Deal
{
    public int Id { get; set; }
    public string ProductName { get; set; } = null!;
    public string Retailer { get; set; } = null!;  // Aldi, Lidl, Rewe, Edeka
    public decimal RegularPrice { get; set; }
    public decimal DealPrice { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string LocationZip { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = null!;  // "manual", "scraper", "api"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public decimal Discount => (RegularPrice - DealPrice) / RegularPrice * 100;

    [NotMapped]
    public bool IsActive => DateTime.UtcNow >= ValidFrom && DateTime.UtcNow <= ValidTo;
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

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255)
                .HasIndex()
                .IsUnique();
            
            entity.Property(e => e.OAuthId)
                .IsRequired()
                .HasMaxLength(500);
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasMany(e => e.Preferences)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserPreference configuration
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
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Indexes for common queries
            entity.HasIndex(e => new { e.Retailer, e.ValidFrom, e.ValidTo });
            entity.HasIndex(e => e.LocationZip);
            entity.HasIndex(e => e.ProductName);
        });
    }
}
```

---

## Dependency Injection Setup

Register `DbContext` in service startup.

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
var builder = WebApplicationBuilder.CreateBuilder(args);

builder.Services.AddDataAccess(builder.Configuration);

var app = builder.Build();
app.Run();
```

---

## Code-First Migrations

All schema changes via migrations. Never edit database directly.

### Initial Migration

```bash
# From service directory
cd services/user-service

# Create initial migration
dotnet ef migrations add InitialCreate \
  --startup-project . \
  --output-dir Data/Migrations

# View generated migration (review!)
cat src/Data/Migrations/20240514120000_InitialCreate.cs

# Update database
dotnet ef database update

# Verify schema
dotnet ef migrations list
```

### Add a New Entity

```bash
# 1. Create entity in Domain/
# 2. Add DbSet to UserDbContext
# 3. Configure in OnModelCreating
# 4. Create migration
dotnet ef migrations add AddNewEntity

# 5. Review migration
# 6. Apply it
dotnet ef database update
```

### Modify Existing Entity

```bash
# Example: Add column to User
# Edit Domain/User.cs

public class User
{
    // ... existing properties ...
    public string? PhoneNumber { get; set; }  // NEW
}

# Create migration (EF detects the change)
dotnet ef migrations add AddPhoneNumberToUser

# Review migration before applying
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

public record CreateUserCommand(string Email, string Name, string OAuthId);
public record CreateUserResult(int UserId, string Email);

public class CreateUserHandler(UserDbContext dbContext, ILogger<CreateUserHandler> logger)
{
    public async Task<CreateUserResult> HandleAsync(CreateUserCommand cmd)
    {
        // Check if user exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == cmd.Email);
        
        if (existingUser != null)
            throw new InvalidOperationException($"User {cmd.Email} already exists");

        // Create new user
        var user = new User
        {
            Email = cmd.Email,
            Name = cmd.Name,
            OAuthId = cmd.OAuthId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created user {UserId} with email {Email}", user.Id, user.Email);

        return new CreateUserResult(user.Id, user.Email);
    }
}

// Features/GetUser/GetUserHandler.cs
namespace ExpenseTracker.UserService.Features.GetUser;

using ExpenseTracker.UserService.Data;

public record GetUserQuery(int UserId);
public record GetUserResult(int Id, string Email, string Name, IEnumerable<(string Key, string Value)> Preferences);

public class GetUserHandler(UserDbContext dbContext)
{
    public async Task<GetUserResult> HandleAsync(GetUserQuery query)
    {
        var user = await dbContext.Users
            .Include(u => u.Preferences)
            .FirstOrDefaultAsync(u => u.Id == query.UserId)
            ?? throw new KeyNotFoundException($"User {query.UserId} not found");

        return new GetUserResult(
            user.Id,
            user.Email,
            user.Name,
            user.Preferences.Select(p => (p.Key, p.Value))
        );
    }
}
```

### Querying

```csharp
// Simple queries
var user = await dbContext.Users
    .FirstOrDefaultAsync(u => u.Email == email);

var activeUsers = await dbContext.Users
    .Where(u => u.IsActive)
    .OrderByDescending(u => u.CreatedAt)
    .ToListAsync();

// With related data
var userWithPrefs = await dbContext.Users
    .Include(u => u.Preferences)
    .FirstOrDefaultAsync(u => u.Id == userId);

// Aggregations
var userCount = await dbContext.Users.CountAsync();

var avgTotalByStore = await dbContext.Receipts
    .GroupBy(r => r.Store)
    .Select(g => new { Store = g.Key, AvgTotal = g.Average(r => r.TotalAmount) })
    .ToListAsync();

// Raw SQL if needed (rarely)
var deals = await dbContext.Deals
    .FromSql($@"
        SELECT * FROM ""Deals""
        WHERE ""LocationZip"" = {zip}
        AND ""ValidFrom"" <= CURRENT_DATE
        AND ""ValidTo"" >= CURRENT_DATE
    ")
    .ToListAsync();
```

---

## Connection String Configuration

### Local Development (.env / appsettings.Development.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres-users;Port=5432;Database=users_db;Username=postgres;Password=localpassword;"
  }
}
```

### Production (environment variables)

```bash
# In docker-compose.prod.yml or k8s secret
DATABASE_URL=Host=postgres-users;Port=5432;Database=users_db;Username=postgres;Password=${SECURE_PASSWORD};
```

### Read from environment in code

```csharp
// Program.cs
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? configuration.GetConnectionString("DefaultConnection");
```

---

## Migration Management

### Generate migration without applying

```bash
dotnet ef migrations add SomeChange --no-build
```

### List all migrations

```bash
dotnet ef migrations list
```

### Remove pending migration (not yet applied)

```bash
dotnet ef migrations remove
```

### Revert to specific migration

```bash
dotnet ef database update NameOfMigration
```

### Generate SQL script

```bash
# See what would be executed
dotnet ef migrations script > migration.sql
cat migration.sql
```

---

## Best Practices

1. **Always review migrations before applying**
   - `dotnet ef migrations add DescriptiveName`
   - Check the `.cs` file in Migrations/
   - Then `dotnet ef database update`

2. **Migrations are versioned code**
   - Check them into git
   - Never edit generated migrations manually
   - If wrong, create new migration to fix it

3. **Use descriptive migration names**
   - Good: `AddPhoneNumberToUser`, `CreateReceiptItemsTable`
   - Bad: `Update`, `Migration20240514`

4. **Entity configurations in OnModelCreating**
   - Indexes
   - Constraints
   - Precision/length limits
   - Relationships

5. **No lazy loading in distributed systems**
   - Always `.Include()` related data you need
   - Services can't rely on navigation properties being loaded

6. **Seed data in migrations if needed**
   ```csharp
   migrationBuilder.InsertData(
       table: "Users",
       columns: new[] { "Email", "Name", "CreatedAt" },
       values: new object[] { "admin@test.com", "Admin", DateTime.UtcNow });
   ```

---

## Troubleshooting

### "No DbContext found"
```bash
# Make sure DbContext is registered in DI
services.AddDbContext<UserDbContext>(options => ...);
```

### "Connection string not found"
```bash
# Check appsettings.json or environment variables
echo $DATABASE_URL
```

### Migration conflicts
```bash
# If multiple migrations on different branches, merge conflicts in migrations snapshot
# Keep both migrations, let EF sort them
# Re-add the migration to update snapshot
dotnet ef migrations add RebaseAfterMerge
```

### Reset local database

```bash
# Remove all migrations and schema
dotnet ef database update 0

# Delete migration files manually from Migrations/
# Recreate initial migration
dotnet ef migrations add InitialCreate
dotnet ef database update
```
