# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Restore and build
dotnet tool restore
dotnet restore
dotnet build

# Run (fastest dev loop)
dotnet watch run

# Database migrations
dotnet ef database update
dotnet ef migrations add <MigrationName>

# Windows service deployment
.\register-windows-service.ps1 -ConnectionString "..." -JwtKey "..." -AdminEmail "..." -AdminPassword "..." -Force
```

No automated test project exists. Use `dotnet build` and manually verify the affected MVC path before changes.

## Local Development Setup

Set user secrets before first run (startup validation throws `OptionsValidationException` if `Admin:Email` or `Admin:Password` is missing):

```powershell
dotnet user-secrets set "ConnectionStrings:database" "Host=localhost;Port=5432;Database=airline_reservation;Username=airline_reserv;Password=change-me" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Jwt:Key" "ErbasAirlinesDemoJwtKey2026_SymmetricSigningKey_ChangeInProduction" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Admin:Email" "admin@example.com" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Admin:Password" "ChangeThisPassword" --project .\AirlineSeatReservationSystem.csproj
```

App runs at `http://localhost:5192/airline` (root `/` redirects to `/airline`).

## Architecture

Single ASP.NET Core MVC app (.NET 8) with PostgreSQL via Entity Framework Core 8.

**Layer structure:**
- `Controllers/` — thin MVC controllers + `FlightApiController` (JWT-protected REST API)
- `Entity/` — domain models: `User`, `Flight`, `Seat`, `Booking`
- `Data/Abstract/` — repository interfaces (`IUserRepository`, `IFlightRepository`, `ISeatRepository`, `IBookingRepository`)
- `Data/Concrete/Efcore/` — EF Core implementations; `EfUserRepository` handles SHA256 password hashing
- `Models/` — view models (separate from domain entities)
- `Services/` — `LanguageService` (localization), `AdminSettings` (config validation)
- `Resources/` — `.resx` localization files for `en-US` and `tr-TR`
- `Migrations/` — EF Core schema history

**Authentication:** Two schemes configured in `Program.cs`:
- Cookie auth for the MVC UI (claims-based sign-in via `UsersController`)
- JWT Bearer for `/api/flight/*` endpoints (`FlightApiController` issues tokens via `/api/flight/GenerateToken`)

**Authorization:** Admin role is assigned at runtime to the user whose email matches `Admin:Email` config. The seeded admin account is created/updated at startup. Admin-only actions use `[Authorize(Roles = "admin")]`.

**Localization:** Language switching sets a culture cookie (`CookieRequestCultureProvider`). The `Referer` header used in language-switch redirects is untrusted — validate before using in production.

**Configuration keys** (env var equivalents use `__` separator):
| Key | Purpose |
|-----|---------|
| `ConnectionStrings:database` | PostgreSQL connection string |
| `Jwt:Key` | HMAC signing key for JWT tokens |
| `Jwt:ExpiryInDays` | Token validity (default 7); missing value breaks token generation |
| `App:Port` | HTTP port (default 5192) |
| `App:BasePath` | URL prefix (default `/airline`) |
| `Admin:Email` | Admin account identity |
| `Admin:Password` | Admin account password (stored as SHA256) |

## Coding Style

- 4-space indentation, braces on their own lines
- `PascalCase` for classes, view models, Razor files, public members; `camelCase` for locals and parameters
- Keep controllers thin — data access belongs in repositories, rendering in Razor views
- Resolve `CS860x` nullable warnings in new code; do not add more
