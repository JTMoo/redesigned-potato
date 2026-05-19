using Microsoft.AspNetCore.Mvc;
using UserService.Application.Exceptions;
using UserService.Application.UseCases;

namespace UserService.Presentation;

[ApiController]
[Route("[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly UpsertUserUseCase _upsertUser;
    private readonly GetUserUseCase _getUser;
    private readonly UpdatePreferencesUseCase _updatePreferences;

    public UsersController(
        UpsertUserUseCase upsertUser,
        GetUserUseCase getUser,
        UpdatePreferencesUseCase updatePreferences)
    {
        ArgumentNullException.ThrowIfNull(upsertUser);
        ArgumentNullException.ThrowIfNull(getUser);
        ArgumentNullException.ThrowIfNull(updatePreferences);
        _upsertUser = upsertUser;
        _getUser = getUser;
        _updatePreferences = updatePreferences;
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (user, wasCreated) = await _upsertUser.ExecuteAsync(
            request.GoogleId, request.Email, request.DisplayName, cancellationToken);

        return wasCreated
            ? StatusCode(StatusCodes.Status201Created, user)
            : Ok(user);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return BadRequest("X-User-Id header is missing or invalid.");

        try
        {
            var user = await _getUser.ExecuteAsync(userId, cancellationToken);
            return Ok(user);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id:guid}/preferences")]
    public async Task<IActionResult> GetPreferences(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var requestingUserId))
            return BadRequest("X-User-Id header is missing or invalid.");

        try
        {
            var prefs = await _updatePreferences.GetForUserAsync(id, requestingUserId, page, pageSize, cancellationToken);
            return Ok(prefs);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id:guid}/preferences")]
    public async Task<IActionResult> PutPreferences(
        Guid id,
        [FromBody] IReadOnlyList<PreferenceRequest> body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!TryGetUserId(out var requestingUserId))
            return BadRequest("X-User-Id header is missing or invalid.");

        try
        {
            var inputs = body.Select(p => new PreferenceInput(p.PreferenceKey, p.Value)).ToList();
            var prefs = await _updatePreferences.ExecuteAsync(id, requestingUserId, inputs, cancellationToken);
            return Ok(prefs);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var header = HttpContext.Request.Headers["X-User-Id"].FirstOrDefault();
        return header is not null && Guid.TryParse(header, out userId);
    }
}

public sealed record UpsertUserRequest(string GoogleId, string Email, string DisplayName);
public sealed record PreferenceRequest(string PreferenceKey, string Value);
