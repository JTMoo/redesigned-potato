using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserService.Application.DTOs;
using UserService.Application.Exceptions;
using UserService.Data;

namespace UserService.Application.UseCases;

public sealed class GetUserUseCase
{
    private readonly UserDbContext _db;
    private readonly ILogger<GetUserUseCase> _logger;

    public GetUserUseCase(UserDbContext db, ILogger<GetUserUseCase> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _logger = logger;
    }

    public async Task<UserDto> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            throw new NotFoundException($"User {userId} not found.");
        }

        _logger.LogInformation("User {UserId} retrieved", userId);
        return new UserDto(user.Id, user.Email, user.DisplayName);
    }
}
