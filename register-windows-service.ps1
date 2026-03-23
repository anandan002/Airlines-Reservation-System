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

Write-Host "Publishing application..."
dotnet publish $projectFile -c $Configuration -r $Runtime --self-contained $selfContained -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

if (Test-Path $InstallRoot) {
    if ($Force) {
        Write-Host "Stopping and removing existing installation at $InstallRoot"

        if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            sc.exe delete $ServiceName | Out-Null
            Start-Sleep -Seconds 2
        }

        Remove-Item -Path $InstallRoot -Recurse -Force
    }
    elseif (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
        throw "Install root '$InstallRoot' already exists. Re-run with -Force to replace it."
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
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe config $ServiceName binPath= "`"$serviceExe`" --contentRoot `"$InstallRoot`"" start= auto | Out-Null
}
else {
    Write-Host "Creating Windows service..."
    sc.exe create $ServiceName binPath= "`"$serviceExe`" --contentRoot `"$InstallRoot`"" start= auto DisplayName= "`"$DisplayName`"" | Out-Null
}

sc.exe description $ServiceName $Description | Out-Null

$serviceRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$environment = [System.Collections.Generic.List[string]]::new()
$environment.Add("ASPNETCORE_ENVIRONMENT=Production")

if ($ConnectionString) {
    $environment.Add("ConnectionStrings__database=$ConnectionString")
}

if ($JwtKey) {
    $environment.Add("Jwt__Key=$JwtKey")
}

if ($AdminEmail) {
    $environment.Add("Admin__Email=$AdminEmail")
}

if ($AdminPassword) {
    $environment.Add("Admin__Password=$AdminPassword")
}

Set-ItemProperty -Path $serviceRegPath -Name Environment -Type MultiString -Value $environment

Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "Service installed and started successfully."
Write-Host "Name: $ServiceName"
Write-Host "Install path: $InstallRoot"
