using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.DTOs;
using UserService.Application.Exceptions;
using UserService.Data;
using UserService.Domain;

namespace UserService.Application.UseCases;

public sealed record PreferenceInput(string PreferenceKey, string Value);

public sealed class UpdatePreferencesUseCase
{
    private readonly UserDbContext _db;
    private readonly ILogger<UpdatePreferencesUseCase> _logger;

    public UpdatePreferencesUseCase(UserDbContext db, ILogger<UpdatePreferencesUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PreferenceDto>> ExecuteAsync(
        Guid userId,
        Guid requestingUserId,
        IReadOnlyList<PreferenceInput> preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        // Ownership check — users can only manage their own preferences
        if (userId != requestingUserId)
        {
            _logger.LogWarning(
                "User {RequestingUserId} attempted to update preferences for {UserId}",
                requestingUserId, userId);
            throw new NotFoundException($"User {userId} not found.");
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            _logger.LogWarning("User {UserId} not found when updating preferences", userId);
            throw new NotFoundException($"User {userId} not found.");
        }

        // Replace all preferences for the user
        var existing = await _db.UserPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.UserPreferences.RemoveRange(existing);

        var newPreferences = preferences.Select(p => new UserPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PreferenceKey = p.PreferenceKey,
            Value = p.Value,
        }).ToList();

        _db.UserPreferences.AddRange(newPreferences);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Preferences replaced for user {UserId} ({Count} items)", userId, newPreferences.Count);

        return newPreferences.Select(p => new PreferenceDto(p.Id, p.PreferenceKey, p.Value)).ToList();
    }

    public async Task<IReadOnlyList<PreferenceDto>> GetForUserAsync(
        Guid userId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId != requestingUserId)
        {
            _logger.LogWarning(
                "User {RequestingUserId} attempted to read preferences for {UserId}",
                requestingUserId, userId);
            throw new NotFoundException($"User {userId} not found.");
        }

        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            _logger.LogWarning("User {UserId} not found when reading preferences", userId);
            throw new NotFoundException($"User {userId} not found.");
        }

        var prefs = await _db.UserPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        return prefs.Select(p => new PreferenceDto(p.Id, p.PreferenceKey, p.Value)).ToList();
    }
}
