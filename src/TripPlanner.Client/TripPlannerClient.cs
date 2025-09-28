using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Client.Abstractions;
using TripPlanner.Client.Errors;
using TripPlanner.Core.Contracts.Common;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;
using TripPlanner.Core.Contracts.Contracts.V1.Trips;
using TripPlanner.Core.Contracts.Contracts.V1.Gear;
using TripPlanner.Core.Contracts.Contracts.V1.Itinerary;
using TripPlanner.Core.Contracts.Contracts.Common.Participants;

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

    public async Task<bool> DeleteTripAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}", ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to delete trip.", ct);
        return false;
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

    public async Task<bool> RenameTripAsync(string tripId, string name, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}", new { name }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        await ThrowIfError(res, "Failed to rename trip.", ct);
        return false;
    }

    // Dates APIs
    public async Task<bool> UnvoteOnDateAsync(string tripId, string dateIso, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/date-votes", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        // Some APIs require body in DELETE; we need to send body, so do a proper request below
        return await UnvoteOnDateWithBodyAsync(tripId, dateIso, ct);
    }

    private async Task<bool> UnvoteOnDateWithBodyAsync(string tripId, string dateIso, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/date-votes")
        {
            Content = JsonContent.Create(new { date = dateIso })
        };
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to unvote date.", ct);
        return false;
    }

    public async Task<IReadOnlyList<(string Date, bool IsChosen, IReadOnlyList<string> ParticipantIds)>?> ListDateVotesAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/date-votes", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<List<DateVoteRow>>(cancellationToken: ct);
        return doc?.Select(x => (x.date, x.isChosen, (IReadOnlyList<string>)x.participantIds)).ToList();
    }

    private sealed record DateVoteRow(string date, bool isChosen, List<string> participantIds);

    public async Task<bool> VoteOnDateProxyAsync(string tripId, string dateIso, string participantId, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/date-votes/proxy", new { date = dateIso, participantId }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to proxy vote on date.", ct);
        return false;
    }

    public async Task<bool> UnvoteOnDateProxyAsync(string tripId, string dateIso, string participantId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/date-votes/proxy")
        {
            Content = JsonContent.Create(new { date = dateIso, participantId })
        };
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to proxy unvote on date.", ct);
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

    // Gear APIs
    public async Task<IReadOnlyList<GearItemDto>?> ListGearAsync(string tripId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"/api/v1/trips/{tripId}/gear", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<GearItemDto>>(cancellationToken: ct);
    }

    public async Task<GearItemDto?> CreateGearItemAsync(string tripId, CreateGearItemRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/gear", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            await ThrowIfError(res, "Failed to create gear item.", ct);
        }
        return await res.Content.ReadFromJsonAsync<GearItemDto>(cancellationToken: ct);
    }

    public async Task<GearItemDto?> UpdateGearItemAsync(string tripId, string gearId, UpdateGearItemRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/gear/{gearId}", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            await ThrowIfError(res, "Failed to update gear item.", ct);
        }
        return await res.Content.ReadFromJsonAsync<GearItemDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteGearItemAsync(string tripId, string gearId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/gear/{gearId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete gear item.", ct);
        return false;
    }

    public async Task<GearItemDto?> CreateGearAssignmentAsync(string tripId, string gearId, CreateGearAssignmentRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/gear/{gearId}/assignments", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            await ThrowIfError(res, "Failed to create gear assignment.", ct);
        }
        return await res.Content.ReadFromJsonAsync<GearItemDto>(cancellationToken: ct);
    }

    public async Task<GearItemDto?> UpdateGearAssignmentAsync(string tripId, string gearId, string assignmentId, CreateGearAssignmentRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/gear/{gearId}/assignments/{assignmentId}", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            await ThrowIfError(res, "Failed to update gear assignment.", ct);
        }
        return await res.Content.ReadFromJsonAsync<GearItemDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteGearAssignmentAsync(string tripId, string gearId, string assignmentId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/gear/{gearId}/assignments/{assignmentId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete gear assignment.", ct);
        return false;
    }

    public async Task<bool> BulkCreateGearAsync(string tripId, BulkCreateGearRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/gear/bulk", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to bulk create gear.", ct);
        return false;
    }

    // Term APIs
    public async Task<bool> ProposeTermAsync(string tripId, string startIso, string endIso, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/term-proposals", new { start = startIso, end = endIso }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.Created) return true;
        await ThrowIfError(res, "Failed to propose term.", ct);
        return false;
    }

    private sealed record TermListRow(Guid termProposalId, string start, string end, Guid createdByUserId, DateTimeOffset createdAt, bool isChosen, int votesCount);

    public async Task<IReadOnlyList<(string TermId, string Start, string End, int Votes, bool IsChosen)>?> ListTermsAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/term-proposals", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<TermListRow>>(cancellationToken: ct);
        return list?.Select(t => (t.termProposalId.ToString("D"), t.start, t.end, t.votesCount, t.isChosen)).ToList();
    }

    public async Task<bool> VoteTermAsync(string tripId, string termId, CancellationToken ct = default)
    {
        using var res = await http.PostAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}/votes", content: null, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to vote term.", ct);
        return false;
    }

    public async Task<bool> UnvoteTermAsync(string tripId, string termId, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/trips/{tripId}/term-proposals/{termId}/votes");
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to unvote term.", ct);
        return false;
    }

    public async Task<bool> ChooseTermAsync(string tripId, string termId, CancellationToken ct = default)
    {
        using var res = await http.PatchAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}", content: null, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to choose term.", ct);
        return false;
    }

    public async Task<bool> DeleteTermAsync(string tripId, string termId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/term-proposals/{termId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete term.", ct);
        return false;
    }

    // Itinerary APIs
    public async Task<IReadOnlyList<DayDto>?> ListDaysAsync(string tripId, CancellationToken ct = default)
    {
        var res = await http.GetAsync($"/api/v1/trips/{tripId}/days", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<DayDto>>(cancellationToken: ct);
    }

    public async Task<DayDto?> CreateDayAsync(string tripId, CreateDayRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/days", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to create day.", ct);
        return await res.Content.ReadFromJsonAsync<DayDto>(cancellationToken: ct);
    }

    public async Task<DayDto?> GetDayAsync(string tripId, string dayId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<DayDto>(cancellationToken: ct);
    }

    public async Task<bool> UpdateDayAsync(string tripId, string dayId, UpdateDayRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to update day.", ct);
        return false;
    }

    public async Task<bool> DeleteDayAsync(string tripId, string dayId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/days/{dayId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete day.", ct);
        return false;
    }

    public async Task<bool> UpdateDayAnchorsAsync(string tripId, string dayId, UpdateDayAnchorsRequest request, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/anchors", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to update day anchors.", ct);
        return false;
    }

    public async Task<IReadOnlyList<DayItemDto>?> ListDayItemsAsync(string tripId, string dayId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/items", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<DayItemDto>>(cancellationToken: ct);
    }

    public async Task<DayItemDto?> CreateDayItemAsync(string tripId, string dayId, CreateDayItemRequest request, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/items", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to create day item.", ct);
        return await res.Content.ReadFromJsonAsync<DayItemDto>(cancellationToken: ct);
    }

    public async Task<DayItemDto?> GetDayItemAsync(string tripId, string itemId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/items/{itemId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<DayItemDto>(cancellationToken: ct);
    }

    public async Task<bool> UpdateDayItemAsync(string tripId, string itemId, UpdateDayItemRequest request, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/items/{itemId}", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to update day item.", ct);
        return false;
    }

    public async Task<bool> DeleteDayItemAsync(string tripId, string itemId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/items/{itemId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete day item.", ct);
        return false;
    }

    public async Task<bool> ReorderDayItemsAsync(string tripId, string dayId, ReorderDayItemsRequest request, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}/days/{dayId}/items/reorder", request, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to reorder day items.", ct);
        return false;
    }

    public async Task<IReadOnlyList<RouteFileDto>?> ListDayRoutesAsync(string tripId, string dayId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<RouteFileDto>>(cancellationToken: ct);
    }

    public async Task<RouteFileDto?> UploadDayRouteAsync(string tripId, string dayId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        if (!string.IsNullOrWhiteSpace(contentType)) fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, name: "file", fileName: fileName);
        using var res = await http.PostAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes", content, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to upload route file.", ct);
        return await res.Content.ReadFromJsonAsync<RouteFileDto>(cancellationToken: ct);
    }

    public async Task<bool> DeleteDayRouteAsync(string tripId, string dayId, int routeId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/days/{dayId}/routes/{routeId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete route file.", ct);
        return false;
    }

    // Participants APIs
    public async Task<IReadOnlyList<ParticipantInfoDto>?> ListParticipantsAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/participants", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<IReadOnlyList<ParticipantInfoDto>>(cancellationToken: ct);
    }

    public async Task<string?> CreatePlaceholderAsync(string tripId, string displayName, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders", new { displayName }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to create placeholder.", ct);
        var doc = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        var idStr = doc != null && doc.TryGetValue("participantId", out var v) ? v?.ToString() : null;
        return idStr;
    }

    public async Task<bool> UpdateParticipantDisplayNameAsync(string tripId, string participantId, string displayName, CancellationToken ct = default)
    {
        using var res = await http.PatchAsJsonAsync($"/api/v1/trips/{tripId}/participants/{participantId}", new { displayName }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.Forbidden) return false;
        await ThrowIfError(res, "Failed to update participant display name.", ct);
        return false;
    }

    public async Task<bool> DeleteParticipantAsync(string tripId, string participantId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/participants/{participantId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.Forbidden) return false;
        await ThrowIfError(res, "Failed to delete participant.", ct);
        return false;
    }

    public async Task<(string code, string url)?> IssueClaimCodeAsync(string tripId, string participantId, int? expiresInMinutes = null, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/placeholders/{participantId}/claim-codes", new { expiresInMinutes }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to issue claim code.", ct);
        var dict = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        if (dict is null) return null;
        var code = dict.TryGetValue("code", out var c) ? c?.ToString() : null;
        var url  = dict.TryGetValue("url", out var u) ? u?.ToString() : null;
        if (code is null || url is null) return null;
        return (code, url);
    }

    public async Task<bool> ClaimPlaceholderAsync(string code, string? displayName = null, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/placeholders/claim", new { code, displayName }, ct);
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        if (res.StatusCode == HttpStatusCode.BadRequest) return false;
        await ThrowIfError(res, "Failed to claim placeholder.", ct);
        return false;
    }

    // Transportation APIs
    private sealed record TransportationRowDto(Guid transportationId, string title, string? description, Guid createdByUserId, DateTimeOffset createdAt, bool isChosen);

    public async Task<IReadOnlyList<(string TransportationId, string Title, string? Description, bool IsChosen)>?> ListTransportationsAsync(string tripId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"/api/v1/trips/{tripId}/transportations", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<TransportationRowDto>>(cancellationToken: ct);
        return list?.Select(t => (t.transportationId.ToString("D"), t.title, t.description, t.isChosen)).ToList();
    }

    public async Task<string?> CreateTransportationAsync(string tripId, string title, string? description, CancellationToken ct = default)
    {
        using var res = await http.PostAsJsonAsync($"/api/v1/trips/{tripId}/transportations", new { title, description }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to create transportation.", ct);
        var dict = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
        var idStr = dict != null && dict.TryGetValue("transportationId", out var v) ? v?.ToString() : null;
        return idStr;
    }

    public async Task<bool> UpdateTransportationAsync(string tripId, string transportationId, string title, string? description, CancellationToken ct = default)
    {
        using var res = await http.PutAsJsonAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}", new { title, description }, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to update transportation.", ct);
        return false;
    }

    public async Task<bool> DeleteTransportationAsync(string tripId, string transportationId, CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}", ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to delete transportation.", ct);
        return false;
    }

    public async Task<bool> ChooseTransportationAsync(string tripId, string transportationId, CancellationToken ct = default)
    {
        using var res = await http.PatchAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/choose", content: null, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (res.StatusCode == HttpStatusCode.NoContent) return true;
        await ThrowIfError(res, "Failed to choose transportation.", ct);
        return false;
    }

    public async Task<bool> UploadTransportationRouteAsync(string tripId, string transportationId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        if (!string.IsNullOrWhiteSpace(contentType)) fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, name: "files", fileName: fileName);
        using var res = await http.PostAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/routes", content, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to upload transportation route.", ct);
        return true;
    }

    public async Task<bool> UploadTransportationDocumentAsync(string tripId, string transportationId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        if (!string.IsNullOrWhiteSpace(contentType)) fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, name: "files", fileName: fileName);
        using var res = await http.PostAsync($"/api/v1/trips/{tripId}/transportations/{transportationId}/documents", content, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return false;
        if (!res.IsSuccessStatusCode) await ThrowIfError(res, "Failed to upload transportation document.", ct);
        return true;
    }
}