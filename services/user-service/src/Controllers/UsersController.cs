using EventContracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Domain;
using Utilities;

namespace UserService.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IDateTimeProvider _clock;

    public UsersController(UserDbContext db, IPublishEndpoint publish, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _publish = publish;
        _clock = clock;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertUserRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.GoogleId == request.GoogleId);

        if (existing is not null)
        {
            existing.DisplayName = request.DisplayName;
            existing.UpdatedAt = _clock.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { existing.Id });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = request.GoogleId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _publish.Publish(new UserCreatedEvent(
            user.Id, user.Email, user.DisplayName, _clock.UtcNow));

        return Ok(new { user.Id });
    }
}

public sealed record UpsertUserRequest(string GoogleId, string Email, string DisplayName);
