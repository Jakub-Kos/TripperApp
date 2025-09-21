using System.Globalization;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal static class TripMap
{
    public static Trip ToAggregate(TripRecord r)
    {
        var participants = r.Participants.Select(p => new UserId(p.ParticipantId));

        var options = r.DateOptions.Select(o =>
            (
                new DateOptionId(o.DateOptionId),
                DateOnly.ParseExact(o.DateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                o.Votes.Select(v => new UserId(v.UserId))
            ));

        var destinations = r.Destinations.Select(d =>
            (
                new DestinationId(d.DestinationId),
                d.Title,
                d.Description,
                d.Images.Select(i => i.Url),
                d.Votes.Select(v => new UserId(v.UserId))
            ));

        return Trip.Rehydrate(
            new TripId(r.TripId),
            r.Name,
            new UserId(r.OrganizerId),
            participants,
            options,
            destinations
        );
    }

    public static TripRecord ToRecord(Trip trip)
    {
        return new TripRecord
        {
            TripId = trip.Id.Value,
            Name = trip.Name,
            OrganizerId = trip.OrganizerId.Value,
            Participants = trip.Participants
                .Select(p => new ParticipantRecord { TripId = trip.Id.Value, UserId = p.Value })
                .ToList(),
            DateOptions = trip.DateOptions.Select(o => new DateOptionRecord
            {
                DateOptionId = o.Id.Value,
                TripId = trip.Id.Value,
                DateIso = o.Date.ToString("yyyy-MM-dd"),
                Votes = o.Votes.Select(v => new DateVoteRecord { DateOptionId = o.Id.Value, UserId = v.Value }).ToList()
            }).ToList(),
            Destinations = trip.DestinationProposals.Select(p => new DestinationRecord
            {
                DestinationId = p.Id.Value,
                TripId = trip.Id.Value,
                Title = p.Title,
                Description = p.Description,
                Images = p.ImageUrls.Select(u => new DestinationImageRecord { Url = u }).ToList(),
                Votes = p.VotesBy.Select(v => new DestinationVoteRecord { UserId = v.Value }).ToList()
            }).ToList()
        };
    }
}