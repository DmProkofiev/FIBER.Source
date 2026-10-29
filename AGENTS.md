# AGENTS.md

Guidance for AI coding agents working in this repository. Keep changes minimal and verified.

## Project overview

- FIBER Source — WPF desktop app (.NET 10, `net10.0-windows`, C#) for local storage and management of persons, companies, events and media.
- Single-project repo: solution `FIBER.Source.slnx` → project `FIBER.Source/FIBER.Source.csproj`.
- Stack: WPF + MVVM (`CommunityToolkit.Mvvm`), EF Core + SQLite (SQLCipher) via `Microsoft.Extensions.Hosting` DI.

## Commands

- Build: `dotnet build FIBER.Source.slnx` — must pass with **0 errors, 0 warnings**.
- Run: `dotnet run --project FIBER.Source` — Windows only (WPF). Single-instance mutex: `FIBER.Source.App`.
- EF Core migrations: `dotnet ef migrations add <Name>` / `dotnet ef database update` (dotnet-ef 10.0.13 is installed).
- No test suite, no lint/format/typecheck, no CI workflows are configured. Verification = clean build.

## Architecture

- Single project; folders act as layers: `Contracts` (Inputs/Summaries), `Models` (entities, Enums, Joins, Helpers), `Interfaces`, `Services` (+ `Search`, `Comparison` strategy subfolders), `Infrastructure` (Data, Hosting), `Migrations`, `ViewModels`, `Views`, `Helpers`, `Resources`.
- DI is set up in `App.xaml.cs` via `Host.CreateApplicationBuilder`; services and VMs are registered there; DbContext via `AddDbContextFactory<FiberSourceDbContext>`.
- `DatabaseService` is not DI-registered; `IdentificationWindow` receives it directly (`new IdentificationWindow(_databaseService)`).
- Search and comparison use strategy pattern: `ISearchStrategy`/`ISearchStrategyFactory`, `IComparisonStrategy`/`IComparisonStrategyFactory` (selected by key, registered in `App.xaml.cs`).
- MVVM via CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`). Business logic and validation live in services, not ViewModels/Views.

## Gotchas

- Runtime DB: `AppContext.BaseDirectory\fiber.db` (SQLCipher, single file, password-protected). Startup applies WAL + FK pragmas and runs EF `MigrateAsync`.
- EF design-time factory creates `<cwd>\Data\DesignTime.db` — migrations evolve that file, not the runtime `fiber.db`.
- Password change uses `PRAGMA rekey`; export uses file copy or `sqlcipher_export('plaintext')`.
- Media temp files (video/audio playback) go to `%Temp%\FIBER\` and are cleaned by `StorageMaintenanceService` (every 10 min) and on exit.
- Unhandled dispatcher exceptions are caught, shown via notification, and marked handled.

## Source of truth

- `FIBER.Source/TechnicalSpecification.md` — authoritative specification. Consult before changing models or service behavior.

## Investigation order

- For unfamiliar areas: start from `App.xaml.cs` → `ViewModels/MainViewModel.cs` → `Services/` → `Infrastructure/`, then drill down. Prefer executable wiring and config over docs; docs win only when the code agrees.
