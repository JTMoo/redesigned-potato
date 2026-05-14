namespace MatchingService.Domain;

public sealed class RecommendationCache
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DealId { get; set; }
    public decimal Score { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
