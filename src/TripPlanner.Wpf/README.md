TripPlanner.Wpf — Developer README

This document is a short, practical guide for developing and running the WPF client of TripPlanner.
It focuses on day‑to‑day tasks for contributors. Keep it concise and up‑to‑date.

Prerequisites
- .NET SDK: 9.0 (the solution targets net9.0 and net9.0-windows)
- IDE: JetBrains Rider (recommended) or Visual Studio 2022+, or VS Code with C# extensions
- OS: Windows (WPF)

Related projects in this repo
- src/TripPlanner.Api — Minimal API backend the WPF client talks to
- src/TripPlanner.Client — Typed HTTP client used by the WPF app
- src/TripPlanner.Wpf — This WPF client (MVVM + CommunityToolkit.Mvvm)
- adapters/core libs under src/TripPlanner.Core.* and src/TripPlanner.Adapters.*

Quick start
1) Start the backend API
   - In Rider: set Startup project to TripPlanner.Api and run.
   - Or from terminal (PowerShell):
     dotnet run --project .\src\TripPlanner.Api
   - API listens on http://localhost:5162 by default (see appsettings / Program.cs).

2) Run the WPF client
   - Ensure TripPlanner.Wpf is the startup project and run.
   - First run: you’ll see a login window. You can register a user in-app.

3) Verify client ↔ API base address
   - The WPF app reads BaseAddress from src/TripPlanner.Wpf/appsettings.json → TripPlanner:BaseAddress.
   - Defaults to http://localhost:5162. Change if your API runs elsewhere.

Configuration
- appsettings.json (WPF):
  {
    "TripPlanner": {
      "BaseAddress": "http://localhost:5162"
    }
  }
- Tokens: The refresh token is stored encrypted per Windows user in %LOCALAPPDATA%\TripperApp\tokens.dat via DPAPI.
  - To force a sign‑out, use the in‑app sign‑out flow (if exposed) or delete the file.

Auth flow (client)
- Login/registration through TripPlanner.Api (email + password).
- WpfAuthState keeps the access token in memory and persists the refresh token via TokenStore.
- On startup, App.xaml.cs attempts a refresh using the stored token before showing MainWindow.

Project structure (WPF)
- App.xaml / App.xaml.cs — app bootstrap, DI container, login gate, main window
- Views/ — XAML windows/pages (MainWindow, dialogs)
- ViewModels/ — UI logic using CommunityToolkit.Mvvm (ObservableObject + [ObservableProperty]/[RelayCommand])
- Converters/ — Small value converters for bindings
- Auth/ — WpfAuthState + TokenStore (DPAPI persistence)

MVVM patterns in use
- Properties: use [ObservableProperty] to auto-generate getters/setters and OnPropertyChanged.
- Commands: use [RelayCommand] on private async methods for clean async command bindings.
- Dependency injection: services configured in App.OnStartup via Microsoft.Extensions.Hosting.
- The TripPlanner client is injected as ITripPlannerClient.

Working with tabs (ViewModels)
- MainViewModel coordinates tabs and loads data for the selected trip.
- Each tab ViewModel exposes LoadAsync(tripId) called when selection changes.
- Keep logic in the ViewModels; Views should be thin.

Running tests
- Domain/Integration tests live under tests/. There are no UI tests for WPF by default.
- Run all tests:
  dotnet test

Common tasks
- Add a new command: decorate a private method with [RelayCommand] and bind in XAML via Command="{Binding YourMethodCommand}".
- Add a new property: use [ObservableProperty] field → generator creates a property and partial change hooks.
- Add a dialog: create a View (Window/UserControl) under Views/, a corresponding ViewModel, and show it from an existing VM with owner set to MainWindow.
- Call the API: prefer ITripPlannerClient abstractions from TripPlanner.Client; avoid hard dependencies on DTO assemblies in WPF when not needed.

Troubleshooting
- 401/403 or cannot fetch data: ensure API is running and BaseAddress matches; verify your login.
- Swagger/DB not ready: TripPlanner.Api ensures DB creation in development; restart API if needed.
- Image not shown: check that ImageUrl is absolute or a valid pack/relative URI.
- Build errors about SDK: confirm you have .NET 9 SDK installed (dotnet --info).

Coding style & guidelines (short)
- Keep ViewModels small, favor private helpers, and avoid code‑behind except for trivial dialog plumbing.
- Use concise XML summaries; avoid over‑commenting obvious code.
- Prefer async/await and non‑blocking UI patterns.

Release builds
- Set configuration to Release in IDE or run:
  dotnet build -c Release

Notes for contributors
- Keep README sections short and actionable. If you add a new feature area (tab/dialog), add a short bullet here describing how to develop/run it.
- If you change the API port/address, update appsettings.json and this README if the default changes.
