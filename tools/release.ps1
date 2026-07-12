# Hardware Monitorin julkaisuskripti: tyhjentää publish-hakemiston, julkaisee
# self-contained-version, allekirjoittaa sovellus-exen, kääntää Inno Setup
# -asennusohjelman ja allekirjoittaa sen. Yksi ainoa reitti julkaisuun, jotta
# vanhentuneita tiedostoja ei jää pakettiin eivätkä versionumerot erkane.
#
#   .\tools\release.ps1                       # koko julkaisu
#   .\tools\release.ps1 -SkipSign             # ilman allekirjoitusta (testi)
#
# Vaatii: .NET 8 SDK, Inno Setup 6, allekirjoitusvarmenne CurrentUser\My.

param(
    [string]$CertThumbprint = '346D869550F3A7BD54FA947E024341C64F729AF8',
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$SkipSign
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $repoRoot 'src\HardwareMonitor.App\HardwareMonitor.App.csproj'
$iss = Join-Path $repoRoot 'installer\setup.iss'
$publishDir = Join-Path $repoRoot 'publish'
$appExe = Join-Path $publishDir 'HardwareMonitor.exe'

# 1) Versiotarkistus: csprojin <Version> ja setup.iss:n MyAppVersion täsmäävät.
$csVersion = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$issVersion = (Select-String -Path $iss -Pattern '#define\s+MyAppVersion\s+"([^"]+)"').Matches.Groups[1].Value
if (-not $csVersion) { throw 'csprojista ei löytynyt <Version>-arvoa.' }
if ($csVersion -ne $issVersion) {
    throw "Versiot eivät täsmää: csproj=$csVersion, setup.iss=$issVersion. Päivitä molemmat."
}
Write-Host "Julkaistava versio: $csVersion"

# 2) Tyhjennä publish (dotnet publish -o ei siivoa vanhentuneita tiedostoja).
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host '1/4 Julkaistaan self-contained-versio (win-x64, Release)...'
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish epäonnistui.' }

# 3) Lisenssitiedostot mukana (csproj kopioi ne; varmistetaan).
foreach ($f in @('LICENSE.txt', 'THIRD-PARTY-NOTICES.md')) {
    if (-not (Test-Path (Join-Path $publishDir $f))) {
        throw "Lisenssitiedosto puuttuu publishista: $f"
    }
}

# 4) Julkaisutarkistus: jokaisella publishin kolmannen osapuolen DLL:llä on
# vastaava rivi THIRD-PARTY-NOTICES.md:ssä (karkea kartoitus nimellä).
$notices = Get-Content (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') -Raw
$knownPrefixes = @('System.', 'Microsoft.', 'Presentation', 'UIAutomation', 'Windows',
    'netstandard', 'mscor', 'clr', 'host', 'api-ms', 'ucrtbase', 'D3D', 'PenImc',
    'vcruntime', 'wpfgfx', 'Accessibility', 'DirectWriteForwarder', 'ReachFramework',
    'msquic', 'createdump', 'dbgshim', 'coreclr', 'HardwareMonitor')
$unmapped = @()
foreach ($dll in Get-ChildItem "$publishDir\*.dll") {
    $base = [IO.Path]::GetFileNameWithoutExtension($dll.Name)
    if ($knownPrefixes | Where-Object { $base.StartsWith($_) }) { continue }
    # Kolmas osapuoli: jonkin nimiosan on löydyttävä NOTICES-tiedostosta.
    # Natiivikirjastojen "lib"-etuliite stripataan (esim. libSkiaSharp → SkiaSharp).
    $token = ($base -split '[.\-]')[0]
    if ($token.StartsWith('lib') -and $token.Length -gt 3) { $token = $token.Substring(3) }
    if ($notices -notmatch [regex]::Escape($token)) { $unmapped += $dll.Name }
}
if ($unmapped.Count -gt 0) {
    throw "THIRD-PARTY-NOTICES.md ei kata näitä DLL:iä: $($unmapped -join ', ')"
}
Write-Host '    Lisenssikartoitus OK.'

$cert = $null
if (-not $SkipSign) {
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $CertThumbprint
    if (-not $cert) { throw "Allekirjoitusvarmennetta ei löytynyt: $CertThumbprint" }
    Write-Host '2/4 Allekirjoitetaan sovellus-exe...'
    $r = Set-AuthenticodeSignature -FilePath $appExe -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($r.Status -ne 'Valid') { throw "App-exen allekirjoitus epäonnistui: $($r.Status)" }
}

Write-Host '3/4 Käännetään asennusohjelma (Inno Setup)...'
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'ISCC.exe (Inno Setup 6) ei löytynyt.' }
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC epäonnistui.' }

$setupExe = Join-Path $repoRoot "installer\Output\HardwareMonitor-Setup-$csVersion.exe"
if (-not (Test-Path $setupExe)) { throw "Asennusohjelmaa ei syntynyt: $setupExe" }

if (-not $SkipSign) {
    Write-Host '4/4 Allekirjoitetaan asennusohjelma...'
    $r = Set-AuthenticodeSignature -FilePath $setupExe -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
    if ($r.Status -ne 'Valid') { throw "Setup-exen allekirjoitus epäonnistui: $($r.Status)" }
}

Write-Host ''
Write-Host "Valmis: $setupExe"
Write-Host "Julkaise GitHubiin: gh release create v$csVersion `"$setupExe`" --target main --title `"Hardware Monitor v$csVersion`" --notes-file <notes>"
