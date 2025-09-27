using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Errors;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
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

    public async Task<IReadOnlyList<TripDto>> ListMyTripsAsync(bool includeFinished = false, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var qs = $"?includeFinished={(includeFinished ? "true" : "false")}&skip={skip}&take={take}";
        var dto = await http.GetFromJsonAsync<List<TripDto>>($"/api/v1/my/trips{qs}", ct);
        return dto ?? [];
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

    public async Task<bool> UpdateTripStatusAsync(string tripId, bool isFinished, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}/status", new { isFinished }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to update trip status.", ct);
        return false;
    }
    
    public async Task<IReadOnlyList<DestinationProposalDto>?> GetDestinationsAsync(string tripId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"/api/v1/trips/{tripId}/destinations", ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<DestinationProposalDto>>(cancellationToken: ct);
    }

    public async Task<string?> ProposeDestinationAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", request, ct);

        if (res.StatusCode == HttpStatusCode.NotFound)
            return null; // trip not found

        // Throw for other non-success codes so VM can display the message
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Propose destination failed: {(int)res.StatusCode} {res.ReasonPhrase}\n{body}");
        }

        // First try response body: { destinationId: "GUID" }
        var stream = await res.Content.ReadAsStreamAsync(ct);
        if (stream.CanRead && stream.Length > 0)
        {
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.TryGetProperty("destinationId", out var idEl))
                return idEl.GetString();
        }

        // Fallback to Location header: /api/v1/trips/{tripId}/destinations/{id}
        var loc = res.Headers.Location;
        if (loc != null)
        {
            var last = loc.Segments?.LastOrDefault();
            if (Guid.TryParse(last?.Trim('/'), out var id))
                return id.ToString("D");
        }

        // As a last resort, return null so VM can show a friendly message
        return null;
    }

    public async Task<bool> VoteDestinationAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations/{destinationId}/votes", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Vote failed: {(int)res.StatusCode} {res.ReasonPhrase}\n{body}");
        }
        return true;
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
