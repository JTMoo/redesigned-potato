namespace Utilities;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
