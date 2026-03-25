[CmdletBinding()]
param(
    [string]$ServiceName = "AirlineSeatReservationSystem",
    [string]$DisplayName = "Airline Seat Reservation System",
    [string]$Description = "ASP.NET Core MVC airline reservation application",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InstallRoot = "C:\Services\AirlineSeatReservationSystem",
    [string]$ConnectionString,
    [string]$JwtKey,
    [string]$AdminEmail,
    [string]$AdminPassword,
    [switch]$FrameworkDependent,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($currentIdentity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell session."
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $projectRoot "publish"
$publishDir = Join-Path $publishRoot "$Configuration\$Runtime"
$projectFile = Join-Path $projectRoot "AirlineSeatReservationSystem.csproj"
$serviceExe = Join-Path $InstallRoot "AirlineSeatReservationSystem.exe"
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }
$serviceRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"

# Read existing registry env vars so they can be preserved if params are omitted
$existingEnv = @{}
if (Test-Path $serviceRegPath) {
    $existingEnvArray = (Get-ItemProperty -Path $serviceRegPath -Name Environment -ErrorAction SilentlyContinue).Environment
    if ($existingEnvArray) {
        foreach ($entry in $existingEnvArray) {
            $parts = $entry.Split('=', 2)
            if ($parts.Length -eq 2) { $existingEnv[$parts[0]] = $parts[1] }
        }
    }
}

Write-Host "Publishing application..."
# Clean the publish directory first to prevent MSBuild from treating previous output as content
if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
dotnet publish $projectFile -c $Configuration -r $Runtime --self-contained $selfContained -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

if (Test-Path $InstallRoot) {
    if ($Force) {
        Write-Host "Stopping and removing existing installation at $InstallRoot"

        if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            Get-Process -Name "AirlineSeatReservationSystem" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            sc.exe delete $ServiceName | Out-Null
            # Wait until the process is fully gone (up to 15 s)
            $waited = 0
            while ((Get-Process -Name "AirlineSeatReservationSystem" -ErrorAction SilentlyContinue) -and $waited -lt 15) {
                Start-Sleep -Seconds 1
                $waited++
            }
        }

        Remove-Item -Path $InstallRoot -Recurse -Force
    }
    elseif (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
        throw "Install root '$InstallRoot' already exists. Re-run with -Force to replace it."
    }
}

# Always stop the service before copying files (handles re-runs without -Force)
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping service before file copy..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Get-Process -Name "AirlineSeatReservationSystem" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $waited = 0
    while ((Get-Process -Name "AirlineSeatReservationSystem" -ErrorAction SilentlyContinue) -and $waited -lt 15) {
        Start-Sleep -Seconds 1
        $waited++
    }
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $InstallRoot -Recurse -Force

if (-not (Test-Path $serviceExe)) {
    throw "Published executable was not found at '$serviceExe'."
}

$serviceExists = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($serviceExists) {
    Write-Host "Updating existing service..."
    sc.exe config $ServiceName binPath= "`"$serviceExe`" --contentRoot `"$InstallRoot`"" start= auto | Out-Null
}
else {
    Write-Host "Creating Windows service..."
    sc.exe create $ServiceName binPath= "`"$serviceExe`" --contentRoot `"$InstallRoot`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
}

sc.exe description $ServiceName $Description | Out-Null

# Build environment variable list, falling back to existing registry values for omitted params
$environment = [System.Collections.Generic.List[string]]::new()
$environment.Add("ASPNETCORE_ENVIRONMENT=Production")

$resolvedConnectionString = if ($ConnectionString) { $ConnectionString } else { $existingEnv["ConnectionStrings__database"] }
$resolvedJwtKey            = if ($JwtKey)           { $JwtKey }           else { $existingEnv["Jwt__Key"] }
$resolvedAdminEmail        = if ($AdminEmail)        { $AdminEmail }        else { $existingEnv["Admin__Email"] }
$resolvedAdminPassword     = if ($AdminPassword)     { $AdminPassword }     else { $existingEnv["Admin__Password"] }

if ($resolvedConnectionString) { $environment.Add("ConnectionStrings__database=$resolvedConnectionString") }
if ($resolvedJwtKey)            { $environment.Add("Jwt__Key=$resolvedJwtKey") }
if ($resolvedAdminEmail)        { $environment.Add("Admin__Email=$resolvedAdminEmail") }
if ($resolvedAdminPassword)     { $environment.Add("Admin__Password=$resolvedAdminPassword") }

# Fail early if required startup config is missing
$missing = @()
if (-not $resolvedAdminEmail)        { $missing += "-AdminEmail" }
if (-not $resolvedAdminPassword)     { $missing += "-AdminPassword" }
if (-not $resolvedConnectionString)  { $missing += "-ConnectionString" }
if (-not $resolvedJwtKey)            { $missing += "-JwtKey" }
if ($missing) {
    throw "Required parameters not supplied and no previous values found in registry: $($missing -join ', '). Pass them explicitly and re-run."
}

Set-ItemProperty -Path $serviceRegPath -Name Environment -Type MultiString -Value $environment

# Quick smoke-test: run the exe directly for 3 seconds to capture startup errors
Write-Host "Smoke-testing executable..."
$envBlock = @{}
foreach ($entry in $environment) {
    $parts = $entry.Split('=', 2)
    if ($parts.Length -eq 2) { $envBlock[$parts[0]] = $parts[1] }
}
$psi = [System.Diagnostics.ProcessStartInfo]::new($serviceExe)
$psi.UseShellExecute        = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError  = $true
$psi.CreateNoWindow         = $true
foreach ($kv in $envBlock.GetEnumerator()) { $psi.EnvironmentVariables[$kv.Key] = $kv.Value }
$proc = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds 3
if ($proc.HasExited) {
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    Write-Warning "Executable exited early (exit code $($proc.ExitCode))."
    if ($stdout) { Write-Host "STDOUT:`n$stdout" }
    if ($stderr) { Write-Host "STDERR:`n$stderr" }
    throw "Service executable failed on startup. Review output above."
} else {
    $proc.Kill()
    Write-Host "Smoke-test passed."
}

Write-Host "Starting service..."
try {
    Start-Service -Name $ServiceName
} catch {
    Write-Warning "Service failed to start: $_"
    Write-Host ""
    Write-Host "Recent .NET Runtime event log entries (full messages):"
    Get-EventLog -LogName Application -Newest 50 -ErrorAction SilentlyContinue |
        Where-Object { $_.Source -eq ".NET Runtime" -or ($_.EntryType -eq "Error" -and $_.Message -match "AirlineSeat") } |
        Select-Object -First 3 |
        ForEach-Object {
            Write-Host "--- [$($_.EntryType)] $($_.TimeGenerated) (Source: $($_.Source)) ---"
            Write-Host $_.Message
        }
    throw
}

Write-Host ""
Write-Host "Service installed and started successfully."
Write-Host "Name: $ServiceName"
Write-Host "Install path: $InstallRoot"
