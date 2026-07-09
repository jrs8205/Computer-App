# run.ps1 — kääntää ja käynnistää Hardware Monitor -sovelluksen PowerShellissä.
#
# Käyttö PowerShell-ikkunassa (projektin juuressa):
#   .\run.ps1                 # tavallinen käynnistys (asInvoker, ei admin)
#   .\run.ps1 -AsAdmin        # käynnistää järjestelmänvalvojana -> näkee kaikki sensorit
#
# Vaatii .NET 8 SDK:n. Tarkista:  dotnet --version
#
# Jos skriptien ajo on estetty, salli se kerran tälle istunnolle:
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

param(
    [switch]$AsAdmin
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$appProject  = Join-Path $projectRoot "src\HardwareMonitor.App\HardwareMonitor.App.csproj"

if ($AsAdmin) {
    Write-Host "Käännetään ja käynnistetään järjestelmänvalvojana (kaikki sensorit näkyviin)..." -ForegroundColor Cyan
    # Buildataan tässä (ei-korotettuna) ja käynnistetään exe suoraan korotettuna —
    # näin ei jää ylimääräistä PowerShell-ikkunaa auki.
    dotnet build (Join-Path $projectRoot "HardwareMonitor.sln") | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Build epäonnistui." }
    $exe = Join-Path $projectRoot "src\HardwareMonitor.App\bin\Debug\net8.0-windows\HardwareMonitor.exe"
    Start-Process -Verb RunAs -FilePath $exe
    return
}

Write-Host "Käännetään ja käynnistetään Hardware Monitor..." -ForegroundColor Cyan
Write-Host "HUOM: ilman admin-oikeuksia moni lämpö-/tuuletin-/jännitesensori jää näkymättä." -ForegroundColor Yellow
Write-Host "      Aja tarvittaessa:  .\run.ps1 -AsAdmin" -ForegroundColor Yellow

dotnet run --project $appProject
