using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TripPlanner.Core.Contracts.Contracts.V1.Destinations;


namespace TripPlanner.Client;


public interface IDestinationsApi
{
    Task<IReadOnlyList<DestinationProposalDto>> ListAsync(string tripId, CancellationToken ct = default);


    /// <returns>DestinationId (as string GUID)</returns>
    Task<string> ProposeAsync(string tripId, ProposeDestinationRequest request, CancellationToken ct = default);


    Task VoteAsync(string tripId, string destinationId, VoteDestinationRequest request, CancellationToken ct = default);
}