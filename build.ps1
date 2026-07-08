# build.ps1 — palauttaa NuGet-paketit ja kääntää koko ratkaisun PowerShellissä.
#
# Käyttö (projektin juuressa):
#   .\build.ps1                       # Debug-käännös
#   .\build.ps1 -Configuration Release
#
# Vaatii .NET 8 SDK:n:  https://dotnet.microsoft.com/download/dotnet/8.0

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$solution = Join-Path $PSScriptRoot "HardwareMonitor.sln"

Write-Host "Palautetaan NuGet-paketit..." -ForegroundColor Cyan
dotnet restore $solution

Write-Host "Käännetään ($Configuration)..." -ForegroundColor Cyan
dotnet build $solution --configuration $Configuration --no-restore

Write-Host "Valmis." -ForegroundColor Green
