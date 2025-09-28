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

    public async Task<bool> SetDateRangeAsync(string tripId, string startIso, string endIso, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/date-range", new { start = startIso, end = endIso }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to set date range.", ct);
        return false;
    }

    public async Task<bool> VoteOnDateAsync(string tripId, string dateIso, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes", new { date = dateIso }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to vote on date.", ct);
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

    public async Task<string?> GetTripDescriptionAsync(string tripId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"/api/v1/trips/{tripId}/description", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var obj = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct);
        if (obj != null && obj.TryGetValue("description", out var desc)) return desc;
        return string.Empty;
    }

    public async Task<(bool ok, bool forbidden)> SetTripDescriptionAsync(string tripId, string description, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}/description", new { description }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return (true, false);
        if (res.StatusCode == HttpStatusCode.NotFound) return (false, false);
        if (res.StatusCode == HttpStatusCode.Forbidden) return (false, true);
        await ThrowIfError(res, "Failed to set description.", ct);
        return (false, false);
    }

    public async Task<(string code, string url)?> CreateInviteAsync(string tripId, int? expiresInMinutes = null, int? maxUses = null, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/invites", new { expiresInMinutes, maxUses }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var dict = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        if (dict is null) return null;
        var code = dict.TryGetValue("code", out var c) ? c?.ToString() : null;
        var url  = dict.TryGetValue("url", out var u) ? u?.ToString() : null;
        if (code is null || url is null) return null;
        return (code, url);
    }

    public async Task<bool> JoinByCodeAsync(string code, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/join", new { code }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.BadRequest) return false;
        await ThrowIfError(res, "Failed to join trip by code.", ct);
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
