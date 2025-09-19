using TripPlanner.Core.Application.Application.Abstractions;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Core.Application.Application.Trips;

public sealed record CreateTripCommand(string Name, string OrganizerId);

public sealed class CreateTripHandler
{
    private readonly ITripRepository _trips;
    private readonly IUnitOfWork _uow;
    public CreateTripHandler(ITripRepository trips, IUnitOfWork uow) { _trips = trips; _uow = uow; }

    public async Task<CreateTripResponse> Handle(CreateTripCommand cmd, CancellationToken ct)
    {
        var organizer = new UserId(Guid.Parse(cmd.OrganizerId));
        var trip = Trip.Create(cmd.Name, organizer);
        await _trips.AddAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);
        var dto = new TripDto(trip.Id.Value.ToString("D"), trip.Name, trip.OrganizerId.Value.ToString("D"));
        return new CreateTripResponse(dto);
    }
}