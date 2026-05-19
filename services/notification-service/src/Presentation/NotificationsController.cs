using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.UseCases;

namespace NotificationService.Presentation;

[ApiController]
[Route("notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly GetNotificationsUseCase _getNotifications;
    private readonly MarkNotificationReadUseCase _markRead;

    public NotificationsController(
        GetNotificationsUseCase getNotifications,
        MarkNotificationReadUseCase markRead)
    {
        ArgumentNullException.ThrowIfNull(getNotifications);
        ArgumentNullException.ThrowIfNull(markRead);
        _getNotifications = getNotifications;
        _markRead = markRead;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "X-User-Id header is required." });

        var result = await _getNotifications.ExecuteAsync(userId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "X-User-Id header is required." });

        var found = await _markRead.ExecuteAsync(id, userId, cancellationToken);
        if (!found)
            return NotFound(new { error = $"Notification {id} not found." });

        return Ok();
    }
}
