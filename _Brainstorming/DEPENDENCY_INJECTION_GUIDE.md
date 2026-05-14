# Dependency Injection Guide: Microsoft DI

Using built-in `Microsoft.Extensions.DependencyInjection` (MS DI). No third-party containers.

---

## Service Registration

### DbContext Registration

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
            options.UseNpgsql(connectionString));

        return services;
    }
}
```

### Application Services Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<UpdateUserPreferencesHandler>();
        services.AddScoped<UserQueryService>();
        services.AddScoped<UserAuthService>();

        return services;
    }
}
```

### Event Handling Registration (MassTransit + RabbitMQ)

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHandling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<DealCreatedEventConsumer>();
            x.AddConsumer<DealUpdatedEventConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqUrl = configuration.GetConnectionString("RabbitMQ")
                    ?? "amqp://guest:guest@localhost:5672";

                cfg.Host(new Uri(rabbitMqUrl));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

---

## Program.cs Setup

```csharp
// Program.cs (.NET 10)
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataAccess(builder.Configuration)
    .AddApplicationServices()
    .AddEventHandling(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();  // Swagger

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseCors("AllowFrontend");
app.UseRouting();
app.MapControllers();
app.MapOpenApi();

app.Run();
```

---

## Lifetime Management

### Scoped (per request) — use for handlers and services

```csharp
services.AddScoped<UserQueryService>();
services.AddScoped<CreateUserHandler>();
```

### Transient (always new) — use for stateless utilities

```csharp
services.AddTransient<IDateTimeProvider, UtcDateTimeProvider>();
```

### Singleton (application lifetime) — use for config and stateless factories

```csharp
services.AddSingleton<IOcrServiceFactory, OcrServiceFactory>();
```

---

## Practical Examples

### Handler with DbContext

```csharp
// Features/CreateUser/CreateUserHandler.cs
public record CreateUserCommand(string Email, string Name, string OAuthId, string OAuthProvider);

public class CreateUserHandler(
    UserDbContext dbContext,
    ILogger<CreateUserHandler> logger)
{
    public async Task<User> HandleAsync(CreateUserCommand cmd)
    {
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

        logger.LogInformation("Created user {UserId}", user.Id);

        return user;
    }
}

services.AddScoped<CreateUserHandler>();
```

### Event Consumer (MassTransit)

```csharp
// Events/DealCreatedEventConsumer.cs
using MassTransit;
using ExpenseTracker.Shared.Contracts.Events;
using ExpenseTracker.NotificationService.Services;

public class DealCreatedEventConsumer(
    INotificationService notificationService,
    ILogger<DealCreatedEventConsumer> logger)
    : IConsumer<DealCreatedEvent>
{
    public async Task Consume(ConsumeContext<DealCreatedEvent> context)
    {
        logger.LogInformation("Processing deal created event: {DealId}", context.Message.DealId);
        await notificationService.SendDealAlertAsync(context.Message);
    }
}

// Registered via MassTransit:
services.AddMassTransit(x =>
{
    x.AddConsumer<DealCreatedEventConsumer>();
    x.UsingRabbitMq(...);
});
```

### Receipt Processing Service (multiple dependencies)

```csharp
// Services/ReceiptProcessingService.cs
public class ReceiptProcessingService(
    ReceiptDbContext dbContext,
    IOcrService ocrService,
    IPublishEndpoint eventBus,
    ILogger<ReceiptProcessingService> logger)
{
    public async Task<Receipt> ProcessReceiptAsync(Stream imageStream, int userId)
    {
        var extractedText = await ocrService.ExtractTextAsync(imageStream);
        var items = ParseItems(extractedText);

        var receipt = new Receipt
        {
            UserId = userId,
            Store = items.FirstOrDefault()?.Store ?? "Unknown",
            Items = items,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Receipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        await eventBus.Publish(new ReceiptCreatedEvent(receipt.Id, receipt.Store, userId));

        logger.LogInformation("Processed receipt {ReceiptId} for user {UserId}", receipt.Id, userId);

        return receipt;
    }

    private List<ReceiptItem> ParseItems(string text) => [];  // stub
}

services.AddScoped<ReceiptProcessingService>();
services.AddScoped<IOcrService, TesseractOcrService>();
```

### OCR Factory (switchable provider)

```csharp
// Services/OcrServiceFactory.cs
public enum OcrProvider { Tesseract, GoogleVision }

public interface IOcrServiceFactory
{
    IOcrService CreateService(OcrProvider provider);
}

public class OcrServiceFactory : IOcrServiceFactory
{
    public IOcrService CreateService(OcrProvider provider) =>
        provider switch
        {
            OcrProvider.Tesseract => new TesseractOcrService(),
            OcrProvider.GoogleVision => new GoogleVisionOcrService(),
            _ => throw new InvalidOperationException($"Unknown OCR provider: {provider}")
        };
}

services.AddSingleton<IOcrServiceFactory, OcrServiceFactory>();
```

### Controller

```csharp
// Controllers/ReceiptsController.cs
[ApiController]
[Route("api/[controller]")]
public class ReceiptsController(
    ReceiptProcessingService processingService,
    ILogger<ReceiptsController> logger)
    : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadReceipt(IFormFile file)
    {
        // API Gateway validates JWT and injects X-User-Id — service trusts this header
        if (!int.TryParse(Request.Headers["X-User-Id"], out var userId))
            return Unauthorized();

        using var stream = file.OpenReadStream();
        var receipt = await processingService.ProcessReceiptAsync(stream, userId);

        return Ok(new { receiptId = receipt.Id, itemCount = receipt.Items.Count });
    }
}
```

---

## Shared Services Across Services

```csharp
// shared/utilities/SharedServiceCollectionExtensions.cs
namespace ExpenseTracker.Shared.Infrastructure;

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
        return services;
    }
}

// In each service's Program.cs
builder.Services.AddSharedServices();
```

---

## Testing with MS DI

```csharp
// Tests/Features/CreateUserHandlerTests.cs
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

public class CreateUserHandlerTests
{
    private readonly ServiceProvider _serviceProvider;

    public CreateUserHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<UserDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));  // unique DB per test
        services.AddScoped<CreateUserHandler>();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateUser_WithValidCommand_ReturnsUserId()
    {
        var handler = _serviceProvider.GetRequiredService<CreateUserHandler>();
        var cmd = new CreateUserCommand("test@example.com", "Test User", "google-sub-123", "google");

        var result = await handler.HandleAsync(cmd);

        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
    }
}
```

---

## Checklist

- [ ] All services registered with appropriate lifetime (Scoped for handlers, Transient for utilities, Singleton for factories)
- [ ] DbContext registered with `AddDbContext`
- [ ] MassTransit consumers registered
- [ ] CORS configured (allow `localhost:3000` in dev)
- [ ] Migrations run on startup via `dbContext.Database.MigrateAsync()`
- [ ] All dependencies injectable via primary constructors
- [ ] No `new` keyword for services (except factories)
- [ ] `X-User-Id` header extracted in controllers (not JWT re-validated in services)
