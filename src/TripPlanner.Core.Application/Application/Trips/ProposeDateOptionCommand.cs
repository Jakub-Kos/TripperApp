using TripPlanner.Core.Domain.Domain.Aggregates;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Command to propose a new date option for a trip.
/// </summary>
public sealed record ProposeDateOptionCommand(string TripId, DateOnly Date);
