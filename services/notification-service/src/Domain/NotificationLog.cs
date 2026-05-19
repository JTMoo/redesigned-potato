namespace NotificationService.Domain;

public sealed class NotificationLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ReceiptId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
