# TripPlanner.Core.Application – Developer README

This package contains the application layer (use-case orchestration) for TripPlanner. It wires domain behavior to repositories, exposes handlers for commands/queries, and maps aggregates to DTOs.

It is intentionally thin: business rules live in the Domain layer; this layer coordinates IO (repositories, unit of work), input validation/parsing, and mapping.


## Target framework
- .NET 9.0


## What lives here
- Application/Abstractions: ports this layer depends on
  - ITripRepository – aggregate persistence and projections
  - IUnitOfWork – transactional boundary for multi-repo writes
  - IUserRepository – user + refresh token store (auth flows)
  - IClock – time abstraction for deterministic code/tests
- Application/Trips
  - Commands: AddParticipant, ProposeDateOption, ProposeDestination, VoteDestination (+ their Command records)
  - Queries: GetTripById, ListDestinations (+ their Query records)
  - CreateTrip, ListTrips, TripMapping utilities

Handlers follow the pattern:
- Validate/parse incoming IDs and primitives
- Load aggregate or call repository projection
- Call domain methods (pure business rules)
- Persist via repository and confirm with IUnitOfWork
- Map to DTOs defined in Core.Contracts


## Key dependencies
- TripPlanner.Core.Domain – domain aggregates and value objects
- TripPlanner.Core.Contracts – public DTOs and request/response contracts
- Adapters (Ef/InMemory) implement the Abstractions interfaces


## Examples
Create a trip
```csharp
var handler = new CreateTripHandler(trips, uow);
var response = await handler.Handle(new CreateTripCommand("Autumn hike", organizerId), ct);
Console.WriteLine(response.Trip.Name);
```

Add a participant
```csharp
var ok = await new AddParticipantHandler(trips, uow)
    .Handle(new AddParticipantCommand(tripId, userId), ct);
if (!ok) { /* invalid IDs or already a participant */ }
```

Propose a destination
```csharp
var id = await new ProposeDestinationHandler(trips, uow)
    .Handle(new ProposeDestinationCommand(tripId, "Alps", "Weekend trip", new[] { "https://img" }), ct);
```

Vote a destination
```csharp
var voted = await new VoteDestinationHandler(trips, uow)
    .Handle(new VoteDestinationCommand(tripId, destinationId, userGuid), ct);
```

Get a trip summary
```csharp
var dto = await new GetTripByIdHandler(trips).Handle(new GetTripByIdQuery(tripId), ct);
```

List destinations for a trip
```csharp
var list = await new ListDestinationsHandler(trips)
    .Handle(new ListDestinationsQuery(tripId), ct);
```


## Conventions used
- File/member ordering: fields → constructor → public methods → private helpers
- XML summaries: concise, meaningful comments; avoid over-documenting obvious code
- Handlers are sealed classes with a single Handle method taking a Command/Query record
- IDs are strings at the application boundary; parse to Guid/VOs early and return null/false on invalid input
- Prefer repository projections (e.g., GetSummaryAsync) when a full aggregate is unnecessary


## DI and registration
This project does not provide built-in DI registration. Register handlers in your composition root and provide concrete implementations for:
- ITripRepository
- IUnitOfWork
- IUserRepository (if using auth flows from this assembly)
- IClock (optional; a system clock adapter is typical)

EF and InMemory adapters in the repository implement these interfaces.


## Building and tests
From repository root:
```bash
# build
dotnet build

# run all tests
dotnet test
```
There are no unit tests in this project; functionality is exercised by Domain and Integration tests.


## Troubleshooting
- Invalid IDs: handlers return null/false without throwing; validate inputs at the API or UI layer
- Changes not persisted: ensure to call IUnitOfWork.SaveChangesAsync after repository updates
- DTO mismatches: keep TripPlanner.Core.Contracts in sync with API and mappings (TripMapping)


## Related projects
- Core.Domain – aggregates and business rules
- Core.Contracts – external DTOs/requests/responses
- Adapters.Persistence.Ef / .InMemory – concrete persistence
- Api – HTTP endpoints that call these handlers
- Client / Wpf – consumers of the API


## License
This repository is for educational/coursework purposes. See the root README for repository‑wide details.
