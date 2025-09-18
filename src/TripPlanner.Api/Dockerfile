# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution + props first for better layer caching
COPY TripPlanner.sln ./
COPY Directory.Build.props ./Directory.Build.props

COPY src/TripPlanner.Core.Domain/TripPlanner.Core.Domain.csproj src/TripPlanner.Core.Domain/
COPY src/TripPlanner.Core.Contracts/TripPlanner.Core.Contracts.csproj src/TripPlanner.Core.Contracts/
COPY src/TripPlanner.Core.Application/TripPlanner.Core.Application.csproj src/TripPlanner.Core.Application/
COPY src/TripPlanner.Adapters.Persistence.Ef/TripPlanner.Adapters.Persistence.Ef.csproj src/TripPlanner.Adapters.Persistence.Ef/
COPY src/TripPlanner.Adapters.Persistence.InMemory/TripPlanner.Adapters.Persistence.InMemory.csproj src/TripPlanner.Adapters.Persistence.InMemory/
COPY src/TripPlanner.Core.Validation/TripPlanner.Core.Validation.csproj src/TripPlanner.Core.Validation/
COPY src/TripPlanner.Client/TripPlanner.Client.csproj src/TripPlanner.Client/
COPY src/TripPlanner.Api/TripPlanner.Api.csproj src/TripPlanner.Api/

RUN dotnet restore src/TripPlanner.Api/TripPlanner.Api.csproj

# Now copy the rest and publish
COPY src/ ./src/
RUN dotnet publish src/TripPlanner.Api/TripPlanner.Api.csproj -c Release -o /app/publish

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Optional: set default connection string for SQLite (can be overridden at runtime)
ENV ConnectionStrings__Default="Data Source=/app/data/tripplanner.db"

# Copy published build
COPY --from=build /app/publish ./

# Expose HTTP
EXPOSE 8080

# Run
ENTRYPOINT ["dotnet", "TripPlanner.Api.dll"]