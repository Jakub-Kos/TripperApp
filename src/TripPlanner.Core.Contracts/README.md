# TripPlanner.Core.Contracts – Developer README

This package defines the public data contracts (DTOs, request/response records) shared between the API, clients, and adapters. It is intentionally simple and free of business logic – only shapes and concise documentation live here.


## Target framework
- .NET 9.0


## What lives here
- Contracts/Common
  - AuthContracts: Register/Login/Refresh requests and responses
  - ErrorContracts: ErrorCodes and ErrorResponse payload
  - Participants: ParticipantInfoDto shared across multiple endpoints
- Contracts/V1/
  - Trips: TripDto, TripSummaryDto, DateOptionDto, and request/response records
  - Trips/TripOverviewContracts: TripOverviewDto and related requests/responses
  - Destinations: DestinationProposalDto and CRUD/voting requests
  - Gear: GearProvisioning enum and related DTOs/requests (including bulk import)
  - Itinerary: Location/Day/Item/Route DTOs and CRUD requests

These types are consumed by:
- TripPlanner.Api (endpoint inputs/outputs)
- TripPlanner.Client and TripPlanner.Wpf (client-side consumption)
- Core.Application (mapping results from repositories/domain to DTOs)


## Conventions
- Keep files small and cohesive: group related DTOs + requests + responses together (DTOs → Responses → Requests).
- Use sealed record types for DTOs/requests/responses unless a mutable class is required.
- Prefer IReadOnlyList<T> for collections exposed publicly; keep existing shapes to avoid breaking changes.
- Use concise XML summaries. Describe intent and semantics; avoid over-documenting trivialities.
- Keep naming stable; these are public contracts.


## Versioning and breaking changes
- Treat this assembly as a public API. Changing property names/types/orders can break API clients.
- Additive changes (adding new optional properties or new request/response types) are preferred.
- If a breaking change is unavoidable, coordinate it across:
  - Core.Application mappings
  - Api endpoint models and swagger
  - Client/Wpf consumers
  - Tests
- Consider versioning new groups under a new folder (e.g., V2) if the change is large.


## Adding a new contract
1. Choose a folder under Contracts (e.g., V1/Trips) or create a new versioned folder.
2. Define DTOs first, then responses, then requests.
3. Add XML docs to each record/class and parameter, keeping them succinct.
4. Use primitive types and strings for IDs at the boundary; parsing happens in Application layer.
5. If reusing cross-cutting shapes (participants, errors), reference the Common contracts.
6. Build the solution and ensure Api/Client compile with the new contracts.


## Building
From repository root:
```bash
# build
dotnet build

# run all tests
dotnet test
```

This project is package-only and has no tests of its own; behavior is exercised by Domain and Integration tests.


## Troubleshooting
- Missing documentation or inconsistent ordering: follow the conventions above and look at existing files for examples.
- DTO mismatch at runtime: update the mapping in Core.Application and verify Api/Client compile.
- Avoid adding dependencies to this project; keep it POCO/record-only.


## License
Coursework/educational use. See the root README for repository‑wide details.
