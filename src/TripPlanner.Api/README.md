# TripPlanner.Api – Developer README

This is the Minimal API backend for TripPlanner. It exposes endpoints for trips, participants, dates/terms, destinations, gear, itinerary, invites, transportation, and authentication. The API is documented via Swagger/OpenAPI and uses EF Core (SQLite) by default.


## Quick start
- Requirements
  - .NET SDK 9.0+
  - PowerShell 7+ (Windows) or any shell
- Run locally
  - pwsh
    dotnet run --project src\TripPlanner.Api
  - Open Swagger UI: http://localhost:5162/swagger
- Health check
  - http://localhost:5162/health


## Project shape (API layer)
- Program.cs: Minimal API composition (DI, middleware, endpoint mapping)
- Startup.cs: Kept for tooling that expects classic Startup; mirrors Program registrations
- Swagger/SwaggerConfig.cs: Swagger/OpenAPI configuration
- Endpoints/*: Minimal API endpoints grouped by area
- Infrastructure/*: Infra helpers (validation filter, time clock, codes)
- Auth/*: JWT options, service registration, token helper


## Configuration
Appsettings live next to the project (appsettings.json, appsettings.Development.json). Key settings:
- ConnectionStrings:Default
  - SQLite file path, default: Data Source=tripplanner.db
- Jwt
  - Issuer, Audience, Key, AccessTokenMinutes, RefreshTokenDays
  - For dev/tooling, a safe fallback key is used if none is provided, but you should set real values.

Override via environment variables (double underscore for nesting):
- ConnectionStrings__Default=Data Source=tripplanner.db
- Jwt__Issuer=https://localhost
- Jwt__Audience=tripplanner
- Jwt__Key=your-strong-dev-secret-32+chars
- Jwt__RefreshPepper=pepper-for-refresh-token-hashing
- ASPNETCORE_ENVIRONMENT=Development


## Authentication model
- JWT Bearer auth for protected endpoints under /api/v1
- Public endpoints under /auth:
  - POST /auth/register — create user
  - POST /auth/login — returns access token + refresh token
  - POST /auth/refresh — exchanges refresh token for new tokens
  - POST /auth/logout — revokes refresh token
- Access token lifetime defaults to Jwt:AccessTokenMinutes (15). Refresh token lifetime defaults to Jwt:RefreshTokenDays (30).
- Refresh tokens are hashed at rest with an optional pepper (Jwt:RefreshPepper). Client must store the raw refresh token.

Example login (PowerShell):
```powershell
$login = Invoke-RestMethod -Method Post http://localhost:5162/auth/login -ContentType application/json -Body '{"email":"user@example.com","password":"pass"}'
$token = $login.accessToken
Invoke-RestMethod http://localhost:5162/api/v1/trips -Headers @{ Authorization = "Bearer $token" }
```


## Database
- EF Core + SQLite by default (Adapters in src/TripPlanner.Adapters.Persistence.Ef)
- In Development, Program.cs ensures database exists (EnsureCreated). No destructive resets.
- During tests, the API uses isolated SQLite files per test host.

Migrations (from repo root):
```powershell
# Add a migration
 dotnet ef migrations add Init -p src/TripPlanner.Adapters.Persistence.Ef -s src/TripPlanner.Api
# Apply migrations
 dotnet ef database update -p src/TripPlanner.Adapters.Persistence.Ef -s src/TripPlanner.Api
```


## Swagger/OpenAPI
- Swagger UI: /swagger
- OpenAPI JSON: /swagger/v1/swagger.json
- Security: Bearer JWT
- Example providers live under Swagger/Examples


## Endpoint map (high level)
- /api/v1 (requires Authorization)
  - Trips: create, get by id, description get/set, list mine
  - Participants: list, update placeholder display name, delete, issue/claim codes
  - Dates: set trip date range, vote on single dates, choose date
  - Terms: propose term ranges, vote (self/proxy)
  - Destinations: propose/list/vote (self/proxy), update/delete
  - Gear: CRUD, bulk operations, assignments
  - Itinerary: days, items, routes CRUD
  - Transportation: CRUD, attach routes/documents, choose
  - Invites: create invite code, join by code, resolve code
- /auth (anonymous)
  - register, login, refresh, logout
- /health (anonymous)

See Program.cs and Endpoints/* for details.


## Running tests
From repository root:
```powershell
# Run all tests
 dotnet test
```
Integration tests will build and run the API in-memory or against a temporary SQLite database.


## Local development tips
- JSON naming is camelCase via System.Text.Json settings in Program/Startup
- All /api/v1 endpoints are grouped via app.MapGroup("/api/v1") and require JWT by default
- When adding new endpoints:
  - Add them to the v1 group for auth
  - Use DTOs from TripPlanner.Core.Contracts when possible
  - Consider adding FluentValidation validators and ValidationFilter<T>
  - Add Swagger summaries, tags, and example providers if helpful
- For quick manual testing, use Swagger UI or the scripts under tools (smoke-api.ps1)


## Troubleshooting
- 401 Unauthorized
  - Missing/expired JWT. Login again and include Authorization: Bearer <token>
- 403 Forbidden
  - You are authenticated but not allowed (e.g., not a trip participant or not the organizer)
- 404 Not Found
  - Entity id or route mismatch, or hidden for security
- DB file locked (Windows)
  - Stop running processes that hold the SQLite file, or delete the *.db file when safe
- Swagger not loading
  - Check that the app started and Swagger is enabled (Development path)


## Licensing
This repository is for educational/coursework purposes. See the root README for repo-wide details.
