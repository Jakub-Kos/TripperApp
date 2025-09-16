# TripperApp
Seminar Advanced C# Project with the aim of providing a handy tool for planning trips (TRIP PlanER)


```bash
dotnet build
```

```bash
 dotnet run --project src/TripPlanner.Api
```

```bash
# POST
Invoke-RestMethod -Method Post -Uri http://localhost:5162/api/v1/trips `
  -ContentType application/json `
  -Body '{"name":"Snezka Hike","organizerId":"00000000-0000-0000-0000-000000000001"}'
```
```bash
# GET
Invoke-RestMethod http://localhost:5162/api/v1/trips
```