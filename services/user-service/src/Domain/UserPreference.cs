namespace UserService.Domain;

public sealed class UserPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PreferenceKey { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}
