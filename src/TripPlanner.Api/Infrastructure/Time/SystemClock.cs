using TripPlanner.Core.Application.Application.Abstractions;

namespace TripPlanner.Api.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}