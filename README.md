# Erbas Airlines Reservation System

ASP.NET Core MVC airline reservation application with PostgreSQL, cookie authentication, localized UI, seat selection, and admin flight management.

## Features

- Flight search and booking flow
- Seat selection per flight
- Cookie-based sign-in and sign-up
- Admin-only flight management UI
- JWT token endpoint for API access
- English and Turkish localization

## Stack

- .NET 8 ASP.NET Core MVC
- Entity Framework Core 8
- PostgreSQL
- Bootstrap 5

## Runtime Configuration

The app reads configuration from `appsettings.json`, environment variables, and local user secrets in development.

Important settings:

- `ConnectionStrings:database`
- `Jwt:Key`
- `Jwt:ExpiryInDays`
- `App:Port`
- `App:BasePath`
- `Admin:Email`
- `Admin:Password`

Minimal example:

```json
{
  "ConnectionStrings": {
    "database": "Host=localhost;Port=5432;Database=airline_reservation;Username=airline_reserv;Password=change-me"
  },
  "Jwt": {
    "Key": "ErbasAirlinesDemoJwtKey2026_SymmetricSigningKey_ChangeInProduction",
    "ExpiryInDays": 7
  },
  "Admin": {
    "Email": "admin@example.com",
    "Password": "ChangeThisPassword"
  },
  "App": {
    "Port": 5192,
    "BasePath": "/airline"
  }
}
```

Current base-path behavior:

- Default base path is `/airline`
- Root `/` redirects to `/airline`
- Typical local URL is `http://localhost:5192/airline`

Equivalent environment variable names:

- `ConnectionStrings__database`
- `Jwt__Key`
- `Jwt__ExpiryInDays`
- `App__Port`
- `App__BasePath`
- `Admin__Email`
- `Admin__Password`

## Local Development

1. Restore tools and packages.

```powershell
dotnet tool restore
dotnet restore
```

2. Set local secrets.

```powershell
dotnet user-secrets set "ConnectionStrings:database" "Host=localhost;Port=5432;Database=airline_reservation;Username=airline_reserv;Password=change-me" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Jwt:Key" "ErbasAirlinesDemoJwtKey2026_SymmetricSigningKey_ChangeInProduction" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Admin:Email" "admin@example.com" --project .\AirlineSeatReservationSystem.csproj
dotnet user-secrets set "Admin:Password" "ChangeThisPassword" --project .\AirlineSeatReservationSystem.csproj
```

Set JWT expiry in `appsettings.json` or with an environment variable such as `Jwt__ExpiryInDays=7`.

3. Apply migrations.

```powershell
dotnet ef database update
```

4. Run the app.

```powershell
dotnet run --project .\AirlineSeatReservationSystem.csproj
```

Open:

- `http://localhost:5192/airline`

## Admin Account

The application seeds one admin account at startup from configuration.

- `Admin:Email` is the admin identity
- `Admin:Password` is the admin password used for the seeded account

Notes:

- There is no hardcoded admin email or password in the app anymore
- Startup validation fails if `Admin:Email` or `Admin:Password` is missing
- Changing the configured admin email or password and restarting the app updates the seeded admin account
- Admin access is assigned by the configured admin email

## Windows Service Deployment

Use the included installer script:

```powershell
.\register-windows-service.ps1 `
  -ConnectionString "Host=localhost;Port=5432;Database=airline_reservation;Username=airline_reserv;Password=change-me" `
  -JwtKey "ErbasAirlinesDemoJwtKey2026_SymmetricSigningKey_ChangeInProduction" `
  -AdminEmail "admin@example.com" `
  -AdminPassword "ChangeThisPassword" `
  -Force
```

Notes:

- The script publishes and installs the app under `C:\Services\AirlineSeatReservationSystem`
- It runs the app as a Windows service
- It injects production config through service environment variables
- The service reads `App:Port`, `App:BasePath`, and `Jwt:ExpiryInDays` from deployed config
- The service requires `Admin__Email` and `Admin__Password`; if omitted, startup fails with an options validation error
- If the machine does not have .NET 8 installed, keep the default self-contained publish mode
- User secrets are not used when the service runs with `ASPNETCORE_ENVIRONMENT=Production`
- After auth or admin-role code changes, restart the service and sign in again to refresh claims

If the service fails to start, check Event Viewer:

- `Applications and Services Logs` or `Windows Logs > Application`
- Look for `.NET Runtime` or `Application Error` entries for `AirlineSeatReservationSystem.exe`
- A missing admin secret appears as `Microsoft.Extensions.Options.OptionsValidationException`

## Production Notes

- Do not keep the demo JWT key in production
- Do not keep demo admin credentials in production
- Rotate the admin password after first login
- Store real secrets outside source control

## Review Notes

- The JWT token endpoint depends on `Jwt:ExpiryInDays`. If it is missing, token generation can fail even when sign-in works.
- The language-switch actions currently redirect using the request `Referer` header. Treat that as untrusted input in production deployments.

## Useful Commands

```powershell
dotnet build .\AirlineSeatReservationSystem.csproj
dotnet ef database update
dotnet run --project .\AirlineSeatReservationSystem.csproj
.\register-windows-service.ps1 -Force
```
