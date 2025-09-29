using System.Net;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Client.Errors;

/// <summary>
/// Exception thrown by the client when an API returns a non-success status with a parsed error payload.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>HTTP status code returned by the API.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Optional structured error payload from the server.</summary>
    public ErrorResponse? Error { get; }

    public ApiException(HttpStatusCode statusCode, string message, ErrorResponse? error = null)
        : base(message)
    {
        StatusCode = statusCode;
        Error = error;
    }
}