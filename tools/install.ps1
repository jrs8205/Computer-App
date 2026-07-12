# Hardware Monitorin paikallinen asennus: julkaisee self-contained-version ja
# asentaa sen Program Filesiin. ACL-suojattu asennuspolku on edellytys sille,
# että autostart-tehtävä saa käynnistää sovelluksen korotettuna (ilman sitä
# sovellus itse kieltäytyy korotuksesta — ks. AutostartService).
#
#   .\tools\install.ps1              # julkaise + asenna + päivitä autostart
#   .\tools\install.ps1 -Uninstall   # poista asennus, tehtävä ja pikakuvake
#
# Vaatii korotetun PowerShellin. Sulje Hardware Monitor (tray → Lopeta) ensin.

param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$taskName = 'HardwareMonitor'
$installDir = Join-Path $env:ProgramFiles 'Hardware Monitor'
$exePath = Join-Path $installDir 'HardwareMonitor.exe'
$shortcutPath = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Hardware Monitor.lnk'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Aja tämä skripti korotetussa PowerShellissä (asennus Program Filesiin vaatii adminin).'
}

if (Get-Process HardwareMonitor -ErrorAction SilentlyContinue) {
    throw 'Hardware Monitor on käynnissä — sulje se ilmaisinalueen valikosta (Lopeta) ensin.'
}

if ($Uninstall) {
    schtasks /Delete /F /TN $taskName 2>$null | Out-Null
    if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
    if (Test-Path $shortcutPath) { Remove-Item $shortcutPath -Force }
    Write-Host "Poistettu: $installDir, ajastettu tehtävä ja Käynnistä-valikon pikakuvake."
    Write-Host "Käyttäjädata säilyi kansiossa $env:LOCALAPPDATA\HardwareMonitor."
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $repoRoot 'publish'

Write-Host '1/4 Julkaistaan self-contained-versio (win-x64, Release)...'
dotnet publish (Join-Path $repoRoot 'src\HardwareMonitor.App\HardwareMonitor.App.csproj') `
    -c Release -r win-x64 --self-contained true -o $publishDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish epäonnistui.' }

Write-Host "2/4 Kopioidaan kansioon $installDir..."
# /MIR poistaa asennuskansiosta vanhentuneet tiedostot; robocopyn koodit 0–7 = onnistui.
robocopy $publishDir $installDir /MIR /NJH /NJS /NDL /NFL | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy epäonnistui (koodi $LASTEXITCODE)." }
$global:LASTEXITCODE = 0

Write-Host '3/4 Päivitetään autostart-tehtävä (jos käytössä)...'
schtasks /Query /TN $taskName > $null 2>&1
if ($LASTEXITCODE -eq 0) {
    # Suojatusta polusta korotus on turvallinen — sama sääntö kuin sovelluksessa.
    # HUOM: sisemmät lainausmerkit backtickillä ILMAN kenoviivaa — PowerShell
    # escapaa ne itse natiivikutsussa (kenoviivat päätyisivät literaaleina schtasksille).
    schtasks /Create /F /RL HIGHEST /SC ONLOGON /TN $taskName /TR "`"$exePath`" --tray" | Out-Null
    Write-Host "    Tehtävä osoittaa nyt asennettuun exeen korotettuna."
} else {
    Write-Host '    Autostart ei ole käytössä — ohitettiin (kytke sovelluksen asetuksista).'
}
$global:LASTEXITCODE = 0

Write-Host '4/4 Luodaan Käynnistä-valikon pikakuvake (käynnistys adminina)...'
$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($shortcutPath)
$lnk.TargetPath = $exePath
$lnk.WorkingDirectory = $installDir
$lnk.Description = 'Windows 11 Hardware Monitor'
$lnk.Save()
# "Suorita järjestelmänvalvojana" -lippu: .lnk-tavun 0x15 bitti 0x20.
$bytes = [IO.File]::ReadAllBytes($shortcutPath)
$bytes[0x15] = $bytes[0x15] -bor 0x20
[IO.File]::WriteAllBytes($shortcutPath, $bytes)

Write-Host ''
Write-Host "Valmis. Asennettu: $exePath"
Write-Host 'Käynnistä Käynnistä-valikosta "Hardware Monitor" (UAC-kysely) —'
Write-Host 'autostart käynnistää sen kirjautuessa korotettuna ilman kyselyä.'
