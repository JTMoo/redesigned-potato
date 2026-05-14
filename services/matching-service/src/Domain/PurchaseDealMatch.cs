namespace MatchingService.Domain;

public sealed class PurchaseDealMatch
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid DealId { get; set; }
    public decimal EstimatedSavings { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
