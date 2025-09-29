using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Api.Infrastructure.Time;

/// <summary>
/// Production implementation of IClock using the system UTC time.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}