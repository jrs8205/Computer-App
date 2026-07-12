# Hardware Monitor

**English** · [Suomeksi](README.fi.md)

A Windows 11 hardware monitor that reads CPU, GPU, memory, disk, motherboard
and fan sensors in real time. What sets it apart: **plain-language logging,
risk analysis and post-crash forensics** — not just raw numbers, but an
answer to "was my machine actually at risk?", plus a machine-context file
you can hand to any AI assistant.

The application UI is available in **Finnish and English**. Project
documentation (`docs/`) is in Finnish by design.

## Features

- **Dashboard**: CPU / GPU / RAM / disks / fans as color-coded cards with a
  plain-language risk summary and recommendation.
- **Desktop overlay**: click-through, always on top; the border color shows
  the worst current state at a glance; position and rows are configurable.
- **History**: 1 s readings aggregated into 5 s min/avg/max rows in SQLite
  (30-day retention), charts from 1 hour to 30 days.
- **Threshold monitoring**: immediate color state, events only after
  sustained exceedance, tray notifications, recovery entries with duration
  and peak.
- **Windows Event Log integration**: Kernel-Power 41, WHEA, display-driver
  and disk errors collected into the same event history.
- **Crash forensics**: an unexpectedly ended session is detected and the
  last known sensor values before the cut are recorded.
- **Reports**: plain-language text report and CSV export (Finnish Excel
  conventions).
- **machine-insights.md**: a continuously regenerated summary of your
  machine's normal levels, trends and events — ready to paste into any AI
  chat as context.
- All UI text colors meet **WCAG AAA** contrast.

## Installation

1. Install the **PawnIO driver**: <https://pawnio.eu/> → `PawnIO_setup.exe`.
   Without it CPU temperatures stay empty on Windows 11 (Windows blocklists
   the old WinRing0 driver; LibreHardwareMonitor 0.9.5+ uses PawnIO).
2. Download the latest **HardwareMonitor-Setup-x.y.z.exe** from
   [Releases](../../releases) and run it. Self-contained — no .NET
   installation required.
3. Launch "Hardware Monitor" from the Start Menu. The app requires
   administrator rights (low-level sensors), so a manual launch shows a UAC
   prompt. Enable **Start with Windows** in the settings and the app starts
   elevated at logon without any prompt (Task Scheduler).

> Note: the installer and app are signed with a self-signed certificate —
> Windows SmartScreen may warn about an unknown publisher. Choose
> "More info" → "Run anyway". The installer closes a running instance
> gracefully, so updating is just running the new setup.

Your data lives under `%LOCALAPPDATA%\HardwareMonitor\` (settings, history
database, logs) and survives updates and uninstall.

## Building from source

Requirements: **Windows 10/11**, **.NET 8 SDK**, PawnIO (see above).

```powershell
dotnet build HardwareMonitor.sln    # full build (0 warnings expected)
dotnet test src/HardwareMonitor.Tests/HardwareMonitor.Tests.csproj
.\run.ps1 -AsAdmin                  # build + run the development build
.\tools\install.ps1                 # local install into Program Files
installer\setup.iss                 # Inno Setup installer definition
```

Notes for contributors:

- `dotnet test` builds Core + tests only — after UI changes run
  `dotnet build HardwareMonitor.sln`.
- Security rule: the autostart task gets elevation (`/RL HIGHEST`) only
  when the exe lives in an ACL-protected path (Program Files) — from a
  user-writable path the task is created without elevation.
- Architecture: `src/HardwareMonitor.Core` (all logic, no UI dependencies,
  unit-tested) + `src/HardwareMonitor.App` (WPF, MVVM without frameworks)
  + `src/HardwareMonitor.Tests` (xUnit).
- Full specification: [`docs/requirements.md`](docs/requirements.md) (Finnish) ·
  progress: [`docs/ROADMAP.md`](docs/ROADMAP.md) · per-feature designs:
  `docs/superpowers/specs/`.

## About

A hobby project by [jrs8205](https://github.com/jrs8205), built for and
tested on a home machine (i9-9900K / RTX 2060 / ASUS Z390-F). The codebase
went through an external bug review before the 1.0 release — all 26
findings were triaged and fixed (`docs/review-triage.md`, in Finnish).

## License

**GPL-3.0** — see [LICENSE](LICENSE). Uses
[LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
(**MPL-2.0**) and [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
(**MIT**).
