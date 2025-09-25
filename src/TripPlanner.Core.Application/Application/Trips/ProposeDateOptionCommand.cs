using TripPlanner.Core.Domain.Domain.Aggregates;

namespace TripPlanner.Core.Application.Application.Trips;

public sealed record ProposeDateOptionCommand(string TripId, DateOnly Date);
