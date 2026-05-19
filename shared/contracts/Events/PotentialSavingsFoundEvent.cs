namespace EventContracts.Events;

public record PotentialSavingsFoundEvent(
    Guid UserId,
    Guid ReceiptId,
    string StoreName,
    int MatchCount,
    decimal TotalSavings
);
