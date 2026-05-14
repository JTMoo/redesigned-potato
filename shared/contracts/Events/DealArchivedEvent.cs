namespace EventContracts.Events;

public record DealArchivedEvent(
    Guid DealId,
    DateTimeOffset OccurredAt
);
