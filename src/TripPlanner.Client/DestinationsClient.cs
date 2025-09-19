using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;


namespace TripPlanner.Client;


internal sealed class DestinationsClient(HttpClient http) : IDestinationsApi
{
    private readonly HttpClient _http = http;

    public async Task<IReadOnlyList<DestinationProposalDto>> ListAsync(string tripId, CancellationToken ct = default)
    {
        var res = await _http.GetFromJsonAsync<List<DestinationProposalDto>>($"/api/v1/trips/{tripId}/destinations", ct);
        return res ?? new List<DestinationProposalDto>();
    }


    public async Task<string> ProposeAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default)
    {
        using var res = await _http.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations", request, ct);
        res.EnsureSuccessStatusCode();
        // API returns { destinationId: "..." }
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("destinationId").GetString()!;
    }


    public async Task VoteAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default)
    {
        using var res = await _http.PostAsJsonAsync($"/api/v1/trips/{tripId}/destinations/{destinationId}/votes", request, ct);
        res.EnsureSuccessStatusCode();
    }
}