namespace AggregationService.Domain;

public sealed class ScrapeJob
{
    public Guid Id { get; set; }
    public Guid DealSourceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int ItemsFound { get; set; }
    public DealSource DealSource { get; set; } = null!;
}
