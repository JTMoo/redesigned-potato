namespace NotificationService.Domain;

public sealed class UserSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
