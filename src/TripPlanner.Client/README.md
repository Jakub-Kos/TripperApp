# TripPlanner.Client – Developer README

This package contains a typed HTTP client for the TripPlanner backend, used by the WPF app and potentially other .NET front‑ends.

It provides a single high‑level entry point `ITripPlannerClient` for most API areas (Trips, Destinations, Terms, Gear, Itinerary, Participants, Transportation, Invites) and a smaller `IDestinationsApi` used in the WPF layer. It also includes DI helpers and an auth delegating handler that attaches/refreshes JWT tokens.


## Target frameworks
- .NET 9.0


## Installation and DI registration
Add a project reference to `TripPlanner.Client` from your application.

Register the client and its auth pipeline in your application’s service container:

```csharp
using TripPlanner.Client;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddTripPlannerClient(opts =>
{
    // Base URL of the TripPlanner API
    opts.BaseAddress = "http://localhost:5162"; // or from configuration
});
```

Notes
- `AddTripPlannerClient` registers:
  - `AuthClient` (bare HttpClient, no auth handler) for auth endpoints.
  - `ITripPlannerClient` (typed HttpClient) for the main API with `AuthHttpMessageHandler`.
  - A default in‑memory implementation of `IAuthState` if you don’t provide your own.

If you also want the narrower Destinations client used by the WPF app, register it too (after calling `AddTripPlannerClient`):

```csharp
using TripPlanner.Client;

services.AddDestinationsClient();
```

`AddDestinationsClient` reads the base address from `IConfiguration` at `TripPlanner:BaseAddress` and reuses the auth handler. Example appsettings.json snippet:

```json
{
  "TripPlanner": {
    "BaseAddress": "http://localhost:5162"
  }
}
```


## Authentication model
- `IAuthState` holds the current tokens used by `AuthHttpMessageHandler`.
- By default, `InMemoryAuthState` is registered. For production, consider a persistent, thread‑safe implementation.
- `AuthClient` is a thin wrapper for `/auth/*` endpoints. It is configured to bypass the auth delegating handler.
- `AuthHttpMessageHandler` attaches the current access token. On a 401 it attempts a single refresh using the refresh token, then safely retries only idempotent GET/HEAD requests.

Basic login flow example:

```csharp
using TripPlanner.Client;
using TripPlanner.Core.Contracts.Common;

var auth = provider.GetRequiredService<AuthClient>();
var state = provider.GetRequiredService<IAuthState>();

var login = await auth.LoginAsync(email, password, ct);
if (login is null)
{
    // invalid credentials
    return;
}

state.SetTokens(login.AccessToken, login.ExpiresInSeconds, login.RefreshToken);

// Now you can call ITripPlannerClient methods authenticated
var api = provider.GetRequiredService<ITripPlannerClient>();
var myTrips = await api.ListMyTripsAsync(ct: ct);
```

Logout example:

```csharp
await auth.LogoutAsync(state.RefreshToken!);
state.Clear();
```


## Usage examples
The main surface is `ITripPlannerClient`. A few examples follow; see the interface for the full list.

Create a trip
```csharp
var trip = await api.CreateTripAsync(new CreateTripRequest
{
    Name = "Autumn hike",
    // other fields...
});
```

List my trips
```csharp
var trips = await api.ListMyTripsAsync(includeFinished: false, skip: 0, take: 50);
```

Destinations – propose and vote
```csharp
// Propose
var destinationId = await api.ProposeDestinationAsync(tripId, new ProposeDestinationRequest
{
    Name = "Alps",
    Description = "Weekend trip"
});

// Vote
var ok = await api.VoteDestinationAsync(tripId, destinationId!, new VoteDestinationRequest { Value = 1 });
```

Using the smaller Destinations client
```csharp
var dest = provider.GetRequiredService<IDestinationsApi>();
var list = await dest.ListAsync(tripId);
```


## Error handling
- Many high‑level methods return bool/null for common outcomes.
- When the server returns an error payload, `TripPlannerClient` throws `TripPlanner.Client.Errors.ApiException` with:
  - `StatusCode`: the HTTP status
  - `Error`: optional structured `ErrorResponse` payload from the server

Example:
```csharp
try
{
    await api.DeleteGearItemAsync(tripId, gearId);
}
catch (ApiException ex)
{
    Console.WriteLine($"API error {ex.StatusCode}: {ex.Message}");
    if (ex.Error is not null)
    {
        Console.WriteLine($"Server code: {ex.Error.Code}, message: {ex.Error.Message}");
    }
}
```


## HTTP retry semantics
- On 401 Unauthorized, the auth handler attempts a single refresh using the stored refresh token.
- After a successful refresh, it retries only idempotent requests without content (GET/HEAD) to avoid reusing disposed content streams.
- Non‑idempotent or content‑bearing requests are not retried automatically.


## Configuration summary
- Base address:
  - `AddTripPlannerClient(opts => opts.BaseAddress = "...")` (recommended), or
  - For the WPF Destinations extension: `IConfiguration["TripPlanner:BaseAddress"]`.
- Provide your own `IAuthState` if you need persistence across app restarts.


## Building and tests
From the repository root:

```bash
# build
dotnet build

# run all tests
dotnet test
```

This client has no project‑specific tests; it is exercised by integration and domain tests in the solution.


## Troubleshooting
- 401 loops or no auth header: ensure you set tokens in `IAuthState` after login and that your `IAuthState` implementation is registered.
- 404s for endpoints: verify the server base address and API version match the client.
- JSON parsing issues: ensure the client and server contracts are in sync (`TripPlanner.Core.Contracts`).


## Related types (quick map)
- `ITripPlannerClient`: high‑level API surface
- `AuthClient`: login/refresh/logout for tokens
- `IAuthState`: token store used by the handler
- `AuthHttpMessageHandler`: attaches/refreshes tokens
- `IDestinationsApi` + `AddDestinationsClient`: narrow Destinations client
- `ServiceCollectionExtensions.AddTripPlannerClient`: registers the main client and auth plumbing


## License
This repository is for educational/coursework purposes. See the root README for repository‑wide details.