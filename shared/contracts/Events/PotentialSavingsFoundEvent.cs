namespace EventContracts.Events;

public record PotentialSavingsFoundEvent(
    Guid UserId,
    Guid ReceiptId,
    Guid MatchedDealId,
    decimal EstimatedSavings,
    DateTimeOffset OccurredAt
);
