using System.Net;
using System.Net.Http.Json;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Errors;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;

namespace TripPlanner.Client;

public sealed class TripPlannerClient(HttpClient http) : ITripPlannerClient
{
    public async Task<TripDto> CreateTripAsync(CreateTripRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync("/api/v1/trips", request, ct);
        if (res.StatusCode == HttpStatusCode.Created)
        {
            var dto = await res.Content.ReadFromJsonAsync<TripDto>(cancellationToken: ct);
            if (dto is null) throw new ApiException(res.StatusCode, "Empty response for CreateTrip.");
            return dto;
        }
        await ThrowIfError(res, "Failed to create trip.", ct);
        throw new ApiException(res.StatusCode, "Unexpected status.");
    }

    public async Task<IReadOnlyList<TripDto>> ListTripsAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var qs = $"?skip={skip}&take={take}";
        var dto = await http.GetFromJsonAsync<List<TripDto>>($"/api/v1/trips{qs}", ct);
        return dto ?? [];
    }

    public async Task<TripSummaryDto?> GetTripByIdAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<TripSummaryDto>(cancellationToken: ct);
    }

    public async Task<bool> AddParticipantAsync(string tripId, AddParticipantRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/participants", request, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to add participant.", ct);
        return false;
    }

    public async Task<string?> ProposeDateOptionAsync(string tripId, ProposeDateRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-options", request, ct);
        if (res.StatusCode == HttpStatusCode.Created)
        {
            var obj = await res.Content.ReadFromJsonAsync<Dictionary<string,string>>(cancellationToken: ct);
            return obj is not null && obj.TryGetValue("dateOptionId", out var id) ? id : null;
        }
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        await ThrowIfError(res, "Failed to propose date option.", ct);
        return null;
    }

    public async Task<bool> CastVoteAsync(string tripId, CastVoteRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/votes", request, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to cast vote.", ct);
        return false;
    }

    private static async Task ThrowIfError(HttpResponseMessage res, string fallbackMessage, CancellationToken ct)
    {
        ErrorResponse? err = null;
        try
        {
            err = await res.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
        }
        catch { /* ignore parse failures */ }

        var msg = err?.Message ?? fallbackMessage;
        throw new ApiException(res.StatusCode, msg, err);
    }
}
