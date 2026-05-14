# Service Dockerfile Template (.NET 10 + C# 14)

Use this template for each service's Dockerfile:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file
COPY ["user-service.csproj", "."]
RUN dotnet restore "user-service.csproj"

# Copy source code
COPY . .

# Build with C# 14
RUN dotnet build "user-service.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "user-service.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "user-service.dll"]
```

## Project File Configuration (*.csproj)

Ensure each service's .csproj specifies C# 14:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MassTransit" Version="8.2.0" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.2.0" />
    <PackageReference Include="EntityFramework" Version="6.4.4" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
  </ItemGroup>

</Project>
```

## Docker Compose Entry

In docker-compose.yml, reference service Dockerfile:

```yaml
user-service:
  build:
    context: ./services/user-service
    dockerfile: Dockerfile
  image: expense-tracker/user-service:latest
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    DATABASE_URL: postgres://postgres:password@postgres-users:5432/users_db
    RABBITMQ_URL: amqp://guest:guest@rabbitmq:5672
    DOTNET_TieredCompilation: true
    DOTNET_TieredCompilationQuickJit: true
  depends_on:
    postgres-users:
      condition: service_healthy
    rabbitmq:
      condition: service_healthy
```

## Running Migrations on Startup

Option 1: Run migrations in Program.cs (recommended for learning)

```csharp
// Program.cs
var app = builder.Build();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

Option 2: Run migrations manually before deployment

```bash
# In Dockerfile or deployment script
RUN dotnet ef database update --startup-project user-service.csproj
```

See DATA_ACCESS_GUIDE.md for migration management details.

## C# 14 Features You Can Use

With C# 14, leverage:

```csharp
// Namespaced file-scoped types
namespace ExpenseTracker.UserService.Domain;

// Records with better pattern matching
public record CreateUserCommand(string Email, string Name, string OAuthId);

// Primary constructors
public class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    public async Task<User> CreateUserAsync(CreateUserCommand cmd)
    {
        logger.LogInformation("Creating user {Email}", cmd.Email);
        return await repository.AddAsync(new User(cmd.Email, cmd.Name, cmd.OAuthId));
    }
}

// Collection expressions
var userIds = new[] { 1, 2, 3 };
var list = [..userIds, 4, 5];

// Enhanced property patterns
public bool IsValidUser(User user) =>
    user is { Email.Length: > 0, OAuthId.Length: > 0 };
```

## Building Locally

```bash
# Build a service image locally
cd services/user-service
docker build -t expense-tracker/user-service:latest .

# Run with docker-compose (auto-builds)
docker-compose build user-service
docker-compose up user-service
```
