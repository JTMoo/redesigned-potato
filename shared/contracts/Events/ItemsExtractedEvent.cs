namespace EventContracts.Events;

public record ExtractedItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice
);

public record ItemsExtractedEvent(
    Guid ReceiptId,
    Guid UserId,
    IReadOnlyList<ExtractedItem> Items,
    DateTimeOffset OccurredAt
);
