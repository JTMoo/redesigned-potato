namespace EventContracts.Events;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string DisplayName,
    DateTimeOffset OccurredAt
);
