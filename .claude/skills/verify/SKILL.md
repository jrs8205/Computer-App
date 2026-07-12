---
name: verify
description: Hardware Monitorin ajonaikainen todennus — build, käynnistys, ikkunoiden ohjaus WM_CLOSElla ja datatiedostojen tarkistus
---

# Hardware Monitorin todennus ajossa

## Build ja käynnistys

```powershell
dotnet build HardwareMonitor.sln     # 0 varoitusta odotettu; vaatii ettei exe ole ajossa
Start-Process "src\HardwareMonitor.App\bin\Debug\net8.0-windows\HardwareMonitor.exe"
```

- Käynnissä oleva HardwareMonitor.exe lukitsee output-DLL:t — mutta `dotnet test`
  buildaa vain Core+Testit, joten testit voi ajaa sovelluksen ajaessa.
- Korotetusta shellistä käynnistetty exe perii korotuksen → CPU-lämmöt näkyvät.
- Single instance: toinen käynnistys poistuu itse ja näyttää ensimmäisen ikkunan.

## Siisti sulkeminen ilman tray-klikkausta

`MinimizeToTray=true` (oletus) estää sulkemisen X:llä — vaihda se testin ajaksi:

```powershell
Copy-Item "$env:LOCALAPPDATA\HardwareMonitor\settings.json" "...\settings.json.bak"
# vaihda "MinimizeToTray": true -> false, käynnistä, ja lopuksi:
# PostMessage(MainWindowHandle, 0x0010, 0, 0) -> siisti sulkeminen (CleanShutdown: true)
# Palauta backup lopuksi! Stop-Process kirjaisi kaatumistapahtuman käyttäjän dataan.
```

## Ikkunoiden löytäminen

FindWindow EI löydä overlayta (tool window) — käytä EnumWindows + GetWindowThreadProcessId
ja suodata PID:llä; overlayn otsikko on 'HardwareMonitor Overlay'. WM_CLOSE = 0x0010.
Huom: PowerShell-istunnon Add-Type-tyypit eivät säily kutsujen välillä.

## Datatiedostot (todennuspinnat)

- `%LOCALAPPDATA%\HardwareMonitor\logs\debug.log` — kiertää 20 MB:ssä → debug.old.log;
  sensorisnapshot minuutin välein eräkirjoituksena (kaikilla riveillä sama aikaleima).
- `%LOCALAPPDATA%\HardwareMonitor\data\history.db` — WAL; lue ReadOnly-yhteydellä
  pienellä dotnet-konsolilla (Microsoft.Data.Sqlite; koneella ei ole sqlite3-CLI:tä).
  5 s koosterivit min/avg/max; onnistuu sovelluksen ajaessa.
- `%LOCALAPPDATA%\HardwareMonitor\data\last_state.json` — CleanShutdown-lippu.
- `%LOCALAPPDATA%\HardwareMonitor\machine-insights.md` — kirjoitus käynnistyksessä
  (odottaa 1. sensoriluennan) + 30 min välein.
