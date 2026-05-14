namespace EventContracts.Events;

public record SavingOpportunityEvent(
    Guid UserId,
    Guid DealId,
    string Title,
    decimal EstimatedSavings,
    DateTimeOffset OccurredAt
);
