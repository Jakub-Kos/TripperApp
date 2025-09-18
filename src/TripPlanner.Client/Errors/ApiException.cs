using System.Net;
using TripPlanner.Core.Contracts.Common;

namespace TripPlanner.Client.Errors;

public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public ErrorResponse? Error { get; }

    public ApiException(HttpStatusCode statusCode, string message, ErrorResponse? error = null)
        : base(message)
    {
        StatusCode = statusCode;
        Error = error;
    }
}