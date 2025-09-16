namespace TripPlanner.Core.Contracts.Common;

public static class ErrorCodes
{
    public const string NotFound   = "not_found";
    public const string Validation = "validation_error";
    public const string Conflict   = "conflict";
    public const string Forbidden  = "forbidden";
}

public sealed record ErrorResponse(string Code, string Message, object? Details = null);