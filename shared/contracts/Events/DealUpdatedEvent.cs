namespace EventContracts.Events;

public record DealUpdatedEvent(
    Guid DealId,
    string Title,
    string Description,
    decimal DiscountAmount,
    string? LocationZip,
    DateTimeOffset OccurredAt
);
