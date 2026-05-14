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
// Infrastructure/ServiceCollectionExtensions.cs (continued)

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register handlers/services
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<UpdateUserPreferencesHandler>();

        // Register services
        services.AddScoped<UserQueryService>();
        services.AddScoped<UserAuthService>();

        return services;
    }
}
```

### Event Handling Registration (MassTransit)

```csharp
// Infrastructure/ServiceCollectionExtensions.cs (continued)

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

### Logging Registration

```csharp
// Infrastructure/ServiceCollectionExtensions.cs (continued)

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogging(this IServiceCollection services)
    {
        services.AddLogging(configure =>
        {
            configure.AddConsole();
            configure.AddDebug();
        });

        return services;
    }
}
```

---

## Program.cs Setup

Register everything in order:

```csharp
// Program.cs
var builder = WebApplicationBuilder.CreateBuilder(args);

// Add services
builder.Services
    .AddDataAccess(builder.Configuration)
    .AddApplicationServices()
    .AddEventHandling(builder.Configuration)
    .AddLogging();

// Add API controllers
builder.Services.AddControllers();
builder.Services.AddOpenApi();  // Swagger

// CORS if needed
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

// Configure pipeline
app.UseCors("AllowFrontend");
app.UseRouting();
app.MapControllers();
app.MapOpenApi();

app.Run();
```

---

## Lifetime Management

MS DI supports three lifetimes:

### Scoped (per request)
```csharp
// New instance per HTTP request
services.AddScoped<UserQueryService>();

// Usage: Inject in controller/handler, used for duration of request
public class GetUserController(UserQueryService service)
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await service.GetUserAsync(id);
        return Ok(user);
    }
}
```

### Transient (always new)
```csharp
// New instance every time it's injected
services.AddTransient<IDateTimeProvider, UtcDateTimeProvider>();

// Use for stateless utilities
public class CreateUserHandler(UserDbContext db, IDateTimeProvider dateTime)
{
    public async Task Handle(CreateUserCommand cmd)
    {
        var user = new User 
        { 
            CreatedAt = dateTime.UtcNow  // Fresh instance each call
        };
    }
}
```

### Singleton (application lifetime)
```csharp
// Single instance for entire application lifetime
services.AddSingleton<IConfiguration>();
services.AddSingleton<ILogger>();

// Rarely use for custom services - can cause threading issues
// OK for: config, factories, stateless utilities
```

---

## Practical Examples

### Handler with DbContext

```csharp
// Features/CreateUser/CreateUserHandler.cs
namespace ExpenseTracker.UserService.Features.CreateUser;

using ExpenseTracker.UserService.Data;
using ExpenseTracker.UserService.Domain;

public record CreateUserCommand(string Email, string Name, string OAuthId);

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
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created user {UserId}", user.Id);

        return user;
    }
}

// Register in ServiceCollectionExtensions
services.AddScoped<CreateUserHandler>();
```

### Event Consumer

```csharp
// Events/DealCreatedEventConsumer.cs
namespace ExpenseTracker.NotificationService.Events;

using MassTransit;
using ExpenseTracker.Shared.Events;
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

// Register in ServiceCollectionExtensions (via MassTransit)
services.AddMassTransit(x =>
{
    x.AddConsumer<DealCreatedEventConsumer>();
    x.UsingRabbitMq((context, cfg) => ...);
});
```

### Service with Multiple Dependencies

```csharp
// Services/ReceiptProcessingService.cs
namespace ExpenseTracker.ReceiptService.Services;

using ExpenseTracker.ReceiptService.Data;
using ExpenseTracker.ReceiptService.Domain;
using MassTransit;

public class ReceiptProcessingService(
    ReceiptDbContext dbContext,
    IOcrService ocrService,
    IPublishEndpoint eventBus,
    ILogger<ReceiptProcessingService> logger)
{
    public async Task<Receipt> ProcessReceiptAsync(Stream imageStream, int userId)
    {
        // Extract text from image
        var extractedText = await ocrService.ExtractTextAsync(imageStream);

        // Parse items
        var items = ParseItems(extractedText);

        // Save receipt
        var receipt = new Receipt
        {
            UserId = userId,
            Store = items.First().Store,
            Items = items,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Receipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        // Publish event for other services
        await eventBus.Publish(new ReceiptCreatedEvent(receipt.Id, receipt.Store, userId));

        logger.LogInformation("Processed receipt {ReceiptId} for user {UserId}", receipt.Id, userId);

        return receipt;
    }

    private List<ReceiptItem> ParseItems(string text) => ...;
}

// Register
services.AddScoped<ReceiptProcessingService>();
services.AddScoped<IOcrService, TesseractOcrService>();
```

### Controller Using Services

```csharp
// Controllers/ReceiptsController.cs
namespace ExpenseTracker.ReceiptService.Controllers;

using ExpenseTracker.ReceiptService.Services;
using Microsoft.AspNetCore.Mvc;

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
        var userId = int.Parse(User.FindFirst("sub")?.Value ?? "0");

        using var stream = file.OpenReadStream();
        var receipt = await processingService.ProcessReceiptAsync(stream, userId);

        return Ok(new { receiptId = receipt.Id, itemCount = receipt.Items.Count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReceipt(int id)
    {
        // Query via DbContext if needed
        logger.LogInformation("Fetching receipt {Id}", id);
        // ...
    }
}
```

---

## Shared Services Across Services

For utilities/contracts used by multiple services, create shared library:

```csharp
// shared/Utilities/ServiceCollectionExtensions.cs
namespace ExpenseTracker.Shared.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

public static class SharedServiceCollectionExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, UtcDateTimeProvider>();
        services.AddTransient<IValidator, RequestValidator>();

        return services;
    }
}

// Then in each service's Program.cs
builder.Services.AddSharedServices();
```

---

## Configuration Management

MS DI integrates with `IConfiguration`:

```csharp
// Access configuration anywhere
public class SomeService(IConfiguration config)
{
    public void DoSomething()
    {
        var setting = config["MySection:MySetting"];
        var connString = config.GetConnectionString("DefaultConnection");
    }
}

// Or use options pattern
public record DealServiceOptions
{
    public int MaxDealsPerUser { get; set; }
    public int CacheDurationMinutes { get; set; }
}

// Register
services.Configure<DealServiceOptions>(configuration.GetSection("DealService"));

// Use with IOptions
public class DealService(IOptions<DealServiceOptions> options)
{
    private readonly DealServiceOptions _options = options.Value;

    public void ApplyLimits() => ...;
}
```

---

## Factory Pattern (if needed)

MS DI doesn't have built-in factories, but easy to create:

```csharp
// Services/OcrServiceFactory.cs
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

// Register
services.AddSingleton<IOcrServiceFactory, OcrServiceFactory>();

// Use
public class ReceiptService(IOcrServiceFactory ocrFactory)
{
    public async Task Process(OcrProvider provider)
    {
        var ocrService = ocrFactory.CreateService(provider);
        // ...
    }
}
```

---

## Testing with MS DI

MS DI works perfectly with xUnit/NUnit:

```csharp
// Tests/Features/CreateUserHandlerTests.cs
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using ExpenseTracker.UserService.Features.CreateUser;
using ExpenseTracker.UserService.Data;

public class CreateUserHandlerTests
{
    private readonly ServiceProvider _serviceProvider;

    public CreateUserHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<UserDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
        services.AddScoped<CreateUserHandler>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateUser_WithValidCommand_ReturnsUserId()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateUserHandler>();
        var cmd = new CreateUserCommand("test@example.com", "Test User", "oauth123");

        // Act
        var result = await handler.HandleAsync(cmd);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
    }
}
```

---

## Checklist

- [ ] All services registered with appropriate lifetime (Scoped for handlers, Transient for utilities, Singleton for config)
- [ ] DbContext registered with `AddDbContext`
- [ ] MassTransit/event consumers registered
- [ ] Logging configured
- [ ] CORS policies configured if needed
- [ ] Migrations run on startup via `app.Services.CreateScope()`
- [ ] All dependencies injectable via constructor (primary constructors)
- [ ] No manual `new` keyword for services (except factories)
