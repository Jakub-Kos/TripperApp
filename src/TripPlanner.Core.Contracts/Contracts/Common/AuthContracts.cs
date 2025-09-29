namespace TripPlanner.Core.Contracts.Contracts.Common;

/// <summary>
/// Request to register a new user.
/// </summary>
/// <param name="Email">User's email which acts as a unique login.</param>
/// <param name="Password">Plain password; must satisfy server-side policy.</param>
/// <param name="DisplayName">Human-friendly display name shown to others.</param>
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

/// <summary>
/// Request to obtain an access token using user credentials.
/// </summary>
/// <param name="Email">User's email.</param>
/// <param name="Password">User's password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Response containing issued JWT access token and refresh token.
/// </summary>
/// <param name="AccessToken">Short-lived bearer token for API calls.</param>
/// <param name="RefreshToken">Long-lived token used to obtain new access tokens.</param>
/// <param name="ExpiresInSeconds">Access token lifetime in seconds.</param>
public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// Request to rotate/refresh the access token using a refresh token.
/// </summary>
/// <param name="RefreshToken">Previously issued refresh token.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Response containing a new access token and rotated refresh token.
/// </summary>
/// <param name="AccessToken">New short-lived access token.</param>
/// <param name="RefreshToken">New refresh token; replace the old one where stored.</param>
/// <param name="ExpiresInSeconds">Access token lifetime in seconds.</param>
public sealed record RefreshResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds);