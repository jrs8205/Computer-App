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
    Write-Host "Käynnistetään järjestelmänvalvojana (kaikki sensorit näkyviin)..." -ForegroundColor Cyan
    $cmd = "dotnet run --project `"$appProject`""
    Start-Process -Verb RunAs -FilePath "powershell.exe" `
        -ArgumentList "-NoExit", "-Command", "cd `"$projectRoot`"; $cmd"
    return
}

Write-Host "Käännetään ja käynnistetään Hardware Monitor..." -ForegroundColor Cyan
Write-Host "HUOM: ilman admin-oikeuksia moni lämpö-/tuuletin-/jännitesensori jää näkymättä." -ForegroundColor Yellow
Write-Host "      Aja tarvittaessa:  .\run.ps1 -AsAdmin" -ForegroundColor Yellow

dotnet run --project $appProject
