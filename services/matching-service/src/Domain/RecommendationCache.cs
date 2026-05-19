namespace MatchingService.Domain;

public sealed class RecommendationCache
{
    public Guid Id { get; set; }
    public Guid DealId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public string? LocationZip { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
