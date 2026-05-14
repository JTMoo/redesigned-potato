namespace NotificationService.Domain;

public sealed class NotificationLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public string Channel { get; set; } = string.Empty;
}
