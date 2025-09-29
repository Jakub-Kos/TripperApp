using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

/// <summary>
/// Handles the creation of a new trip and ensures the organizer is added as a participant.
/// </summary>
public sealed class CreateTripHandler
{
    // Dependencies
    private readonly ITripRepository _trips;
    private readonly IUnitOfWork _uow;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTripHandler"/>.
    /// </summary>
    public CreateTripHandler(ITripRepository trips, IUnitOfWork uow)
    {
        _trips = trips;
        _uow = uow;
    }

    /// <summary>
    /// Creates a trip and persists it. Organizer is also added as a participant.
    /// </summary>
    public async Task<CreateTripResponse> Handle(CreateTripCommand cmd, CancellationToken ct)
    {
        var organizer = new UserId(cmd.OrganizerId);
        var trip = Trip.Create(cmd.Name, organizer);

        // Persist the trip first to ensure a valid TripId exists before adding relations
        await _trips.AddAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);

        // Also add the organizer as a participant so they can manage and vote within the trip
        await _trips.AddParticipant(trip.Id, organizer, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = new TripDto(trip.Id.Value.ToString("D"), trip.Name, trip.OrganizerId.Value.ToString("D"));
        return new CreateTripResponse(dto);
    }
}