namespace TripPlanner.Core.Contracts.Common;

/// <summary>
/// Common application-level error codes used across API responses.
/// Keep these stable; they are part of the public contract.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Input failed validation (missing/invalid fields).</summary>
    public const string Validation = "validation_error";

    /// <summary>Authenticated but not allowed to perform the operation.</summary>
    public const string Forbidden  = "forbidden";

    /// <summary>Requested resource does not exist.</summary>
    public const string NotFound   = "not_found";

    /// <summary>Request conflicts with current state (e.g., duplicate).</summary>
    public const string Conflict   = "conflict";
}

/// <summary>
/// Standard error payload returned by API endpoints.
/// </summary>
/// <param name="Code">One of <see cref="ErrorCodes"/> values.</param>
/// <param name="Message">Human-readable error description.</param>
/// <param name="Details">Optional structured details (e.g., validation errors).</param>
public sealed record ErrorResponse(string Code, string Message, object? Details = null);