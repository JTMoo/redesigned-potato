namespace EventContracts.Events;

public record ReceiptCreatedEvent(
    Guid ReceiptId,
    Guid UserId,
    string StoreName,
    decimal TotalAmount,
    DateTimeOffset OccurredAt
);
