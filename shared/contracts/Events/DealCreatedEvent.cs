namespace EventContracts.Events;

public record DealCreatedEvent(
    Guid DealId,
    string Title,
    string Description,
    decimal DiscountAmount,
    string? LocationZip,
    DateTimeOffset OccurredAt
);
