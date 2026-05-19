using EventContracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.DTOs;
using UserService.Data;
using UserService.Domain;
using Utilities;

namespace UserService.Application.UseCases;

public sealed class UpsertUserUseCase
{
    private readonly UserDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<UpsertUserUseCase> _logger;

    public UpsertUserUseCase(
        UserDbContext db,
        IPublishEndpoint publish,
        IDateTimeProvider clock,
        ILogger<UpsertUserUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _publish = publish;
        _clock = clock;
        _logger = logger;
    }

    public async Task<(UserDto User, bool WasCreated)> ExecuteAsync(
        string googleId,
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(googleId);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);

        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

        if (existing is not null)
        {
            existing.DisplayName = displayName;
            existing.Email = email;
            existing.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} updated (GoogleId: {GoogleId})", existing.Id, googleId);
            return (new UserDto(existing.Id, existing.Email, existing.DisplayName), false);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = googleId,
            Email = email,
            DisplayName = displayName,
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created (GoogleId: {GoogleId})", user.Id, googleId);

        await _publish.Publish(
            new UserCreatedEvent(user.Id, user.Email, user.DisplayName, _clock.UtcNow),
            cancellationToken);

        return (new UserDto(user.Id, user.Email, user.DisplayName), true);
    }
}
