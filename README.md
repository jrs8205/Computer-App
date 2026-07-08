# Windows 11 Hardware Monitor

Windows 11 -tietokoneen laitteistomonitori, joka lukee reaaliajassa CPU:n, GPU:n,
muistin, levyjen, emolevyn ja tuulettimien tietoja. Ohjelman tärkein erottuva idea
on **selkeä lokitus, riskianalyysi ja kaatumisten jälkiselvitys** — ei pelkkiä numeroita,
vaan tieto siitä, oliko kone oikeasti riskirajoilla.

Täysi määrittely: [`docs/requirements.md`](docs/requirements.md).
Kehityksen eteneminen ja seuraavat askeleet: [`docs/ROADMAP.md`](docs/ROADMAP.md).

---

## Missä mennään nyt

**Vaihe 1 / Proof of concept (määrittelyn luku 33) on valmis:**

- C#-ratkaisu, WPF-työpöytäsovellus + erillinen Core-kirjasto
- LibreHardwareMonitorLib mukana
- Näyttää **kaikki koneesta löytyvät sensorit** puuna laitteittain
- Päivittää arvot **1 sekunnin välein**
- Kirjoittaa sensorit myös **debug-lokiin**

Tämä versio vain **lukee, näyttää ja lokittaa** — se ei muuta koneen asetuksia.

---

## Vaatimukset

- **Windows 10 / 11** (WPF on Windows-only)
- **.NET 8 SDK** — lataa: <https://dotnet.microsoft.com/download/dotnet/8.0>
  - Tarkista asennus: `dotnet --version` (pitäisi näyttää 8.x)

---

## Käynnistys PowerShellissä

Avaa PowerShell projektin juuressa (`Computer-App`).

Jos skriptien ajo on estetty, salli se kerran tälle istunnolle:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### Nopein tapa

```powershell
.\run.ps1              # kääntää ja käynnistää (ilman admin-oikeuksia)
.\run.ps1 -AsAdmin     # käynnistää järjestelmänvalvojana -> KAIKKI sensorit näkyviin
```

### Tai suoraan dotnet-komennoilla

```powershell
dotnet restore
dotnet build
dotnet run --project .\src\HardwareMonitor.App\HardwareMonitor.App.csproj
```

### Pelkkä käännös

```powershell
.\build.ps1                        # Debug
.\build.ps1 -Configuration Release # Release
```

---

## Admin-oikeudet ja sensorit

Suunnitelman turvallisuusperiaatteen mukaisesti ohjelma käynnistyy **ilman
admin-oikeuksia**. Silloin näkyvät perustiedot (kuormat, muisti, levykäyttö),
mutta **monet lämpötila-, tuuletin- ja jännitesensorit jäävät piiloon**.

Kun haluat nähdä kaikki sensorit (CPU-lämpö, tuulettimet RPM, jännitteet):

- aja `.\run.ps1 -AsAdmin`, **tai**
- käynnistä PowerShell "Suorita järjestelmänvalvojana" ja aja `dotnet run …`.

Debug-loki tallentuu polkuun:
`%LOCALAPPDATA%\HardwareMonitor\logs\debug.log`
(polku näkyy myös ohjelman alapalkissa).

---

## Projektin rakenne

```text
Computer-App/
  HardwareMonitor.sln
  build.ps1                     # kääntää ratkaisun (PowerShell)
  run.ps1                       # kääntää + käynnistää (PowerShell)
  src/
    HardwareMonitor.Core/       # laiteriippumaton logiikka (ei UI:ta)
      Sensors/
        SensorService.cs        # lukee sensorit LibreHardwareMonitorLib:llä
        UpdateVisitor.cs        # päivittää laitteet ennen lukemista
        SensorReading.cs        # yksi sensorilukema (record)
        HardwareGroup.cs        # laitteen sensorit ryhmänä
      Logging/
        DebugLogger.cs          # debug-loki (tiedosto + Debug-ikkuna)
    HardwareMonitor.App/        # WPF-käyttöliittymä (MVVM)
      MainWindow.xaml           # sensoripuu-näkymä
      ViewModels/               # MainViewModel, HardwareViewModel, SensorViewModel
      app.manifest              # oikeustaso (asInvoker / requireAdministrator)
  docs/
    requirements.md             # koko määrittely
    ROADMAP.md                  # kehitysjärjestys ja seuraavat askeleet
```

---

## Lisenssihuomio

Ohjelma käyttää **LibreHardwareMonitorLib**-kirjastoa (lisenssi **MPL-2.0**).
Oma koodi ja käyttöliittymä ovat erillisiä; MPL koskee kirjaston omia tiedostoja,
jos niitä muokataan. Katso määrittelyn luku 11.
