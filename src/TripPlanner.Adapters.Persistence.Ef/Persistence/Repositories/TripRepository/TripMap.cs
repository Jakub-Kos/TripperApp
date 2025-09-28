using System.Globalization;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Date;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Destination;
using TripPlanner.Adapters.Persistence.Ef.Persistence.Models.Trip;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;
using TripPlanner.Core.Domain.Domain.Aggregates;
using TripPlanner.Core.Domain.Domain.Primitives;

namespace TripPlanner.Adapters.Persistence.Ef.Persistence.Repositories;

internal static class TripMap
{
    public static Trip ToAggregate(TripRecord r)
    {
        // In the aggregate we reuse UserId primitive to carry ParticipantId values.
        var participants = r.Participants.Select(p => new UserId(p.ParticipantId));

        var options = r.DateOptions.Select(o =>
            (
                new DateOptionId(o.DateOptionId),
                DateOnly.ParseExact(o.DateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                // VOTES BY PARTICIPANT
                o.Votes.Select(v => new UserId(v.ParticipantId)),
                o.IsChosen
            ));

        var destinations = r.Destinations.Select(d =>
            (
                new DestinationId(d.DestinationId),
                d.Title,
                d.Description,
                d.Images.Select(i => i.Url),
                // VOTES BY PARTICIPANT
                d.Votes.Select(v => new UserId(v.ParticipantId)),
                d.IsChosen
            ));

        return Trip.Rehydrate(
            new TripId(r.TripId),
            r.Name,
            new UserId(r.OrganizerId),   // (organizer user id stays as user)
            participants,                // list of participant ids carried in UserId primitive
            options,
            destinations,
            r.StartDate,
            r.EndDate
        );
    }

    public static TripRecord ToRecord(Trip trip)
    {
        return new TripRecord
        {
            TripId = trip.Id.Value,
            Name = trip.Name,
            OrganizerId = trip.OrganizerId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            DescriptionMarkdown = "",
            StartDate = trip.StartDate,
            EndDate = trip.EndDate,

            // IMPORTANT: The aggregate's Participants contain participant IDs now.
            // We persist them as ParticipantRecord with UserId = null (placeholder by default).
            // In practice, most participants will be added through commands (AddParticipant/AddPlaceholder),
            // so you may also decide to leave this empty and let commands populate Participants.
            Participants = trip.Participants
                .Select(pid => new ParticipantRecord
                {
                    TripId = trip.Id.Value,
                    ParticipantId = pid.Value,
                    UserId = null,
                    IsPlaceholder = true,
                    DisplayName = "Participant"
                })
                .ToList(),

            DateOptions = trip.DateOptions.Select(o => new DateOptionRecord
            {
                DateOptionId = o.Id.Value,
                TripId = trip.Id.Value,
                DateIso = o.Date.ToString("yyyy-MM-dd"),
                IsChosen = o.IsChosen,
                // VOTES BY PARTICIPANT
                Votes = o.Votes.Select(v => new DateVoteRecord
                {
                    DateOptionId = o.Id.Value,
                    ParticipantId = v.Value
                }).ToList()
            }).ToList(),

            Destinations = trip.DestinationProposals.Select(p => new DestinationRecord
            {
                DestinationId = p.Id.Value,
                TripId = trip.Id.Value,
                Title = p.Title,
                Description = p.Description,
                IsChosen = p.IsChosen,
                Images = p.ImageUrls.Select(u => new DestinationImageRecord { Url = u }).ToList(),
                // VOTES BY PARTICIPANT
                Votes = p.VotesBy.Select(v => new DestinationVoteRecord
                {
                    DestinationId = p.Id.Value,
                    ParticipantId = v.Value
                }).ToList()
            }).ToList()
        };
    }

    public static ParticipantDto ToDto(ParticipantRecord p) =>
        new(
            p.ParticipantId.ToString(),
            p.DisplayName,
            p.IsPlaceholder,
            p.UserId ?? Guid.Empty
        );
}
