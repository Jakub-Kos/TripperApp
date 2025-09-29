using Swashbuckle.AspNetCore.Filters;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Api.Swagger.Examples;

// Example providers used by Swagger to illustrate request/response payloads.
public sealed class CreateTripRequestExample : IExamplesProvider<CreateTripRequest>
{
    public CreateTripRequest GetExamples() =>
        new("Snezka Hike");
}

public sealed class TripDtoExample : IExamplesProvider<TripDto>
{
    public TripDto GetExamples() =>
        new("8c8f2d3b-2b60-4d96-9c0d-9b0e7a1a1111", "Snezka Hike", "00000000-0000-0000-0000-000000000001");
}

public sealed class TripSummaryDtoExample : IExamplesProvider<TripSummaryDto>
{
    public TripSummaryDto GetExamples() =>
        new(
            "8c8f2d3b-2b60-4d96-9c0d-9b0e7a1a1111",
            "Snezka Hike",
            "00000000-0000-0000-0000-000000000001",
            "A beautiful hike to Snezka. Bring warm clothes.",
            "2025-09-27T18:10:00.0000000+00:00",
            false,
            new[] { "00000000-0000-0000-0000-000000000002" }
        );
}

public sealed class AddParticipantRequestExample : IExamplesProvider<AddParticipantRequest>
{
    public AddParticipantRequest GetExamples() =>
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Example Name");
}

public sealed class ProposeDateRequestExample : IExamplesProvider<ProposeDateRequest>
{
    public ProposeDateRequest GetExamples() =>
        new("2025-10-11");
}

public sealed class CastVoteRequestExample : IExamplesProvider<CastVoteRequest>
{
    public CastVoteRequest GetExamples() =>
        new("2b8f4a2e-8c3a-4f3e-8f1d-1c2b3a4d5555", "00000000-0000-0000-0000-000000000002");
}

public sealed class ErrorResponseExample : IExamplesProvider<ErrorResponse>
{
    public ErrorResponse GetExamples() =>
        new(ErrorCodes.Validation, "Validation failed.",
            new Dictionary<string, string[]> { ["Name"] = new[] { "Name is required." } });
}