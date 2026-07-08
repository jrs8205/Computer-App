# Kehityksen eteneminen (ROADMAP)

Perustuu määrittelyn lukuun 32 "Kehitysjärjestys". Tähän merkitään mitä on tehty
ja mitä tehdään seuraavaksi, jotta työtä on helppo jatkaa esimerkiksi illalla.

## ✅ Vaihe 1 — Perusprojekti + proof of concept (VALMIS)

- [x] C# WPF -ratkaisu (`HardwareMonitor.sln`)
- [x] Core-kirjasto + App erillään
- [x] LibreHardwareMonitorLib lisätty
- [x] Näkymä, joka listaa kaikki löydetyt sensorit puuna
- [x] Arvojen päivitys 1 s välein
- [x] Sensorit debug-lokiin
- [x] PowerShell-skriptit (`build.ps1`, `run.ps1`)

**Ensimmäinen onnistumisen mittari (luku 33):** ohjelma löytää CPU:n, GPU:n, levyt,
lämpötilat, kellot, kuormat ja tuulettimet niin hyvin kuin laitteisto sallii.

✅ **Testattu kotikoneella 8.7.2026** (i9-9900K, RTX 2060, ASUS Z390-F, Win 11):
kaikki löytyi — CPU-lämmöt/kellot/kuormat, GPU + hotspot + VRAM, NVMe/SATA-lämmöt
ja SMART, Nuvoton-tuulettimet RPM, Vcore, VRM/Chipset-lämmöt.

**Tärkeä havainto testistä:** Windows 11:n haavoittuvien ajurien estolista blokkaa
WinRing0:n (LibreHardwareMonitorLib 0.9.4) → CPU/emolevysensorit tyhjiä myös adminina.
Korjaus: kirjasto päivitetty **0.9.6**:een + asennettu **PawnIO-ajuri** (pawnio.eu).
Molemmat tarvitaan. Katso README "Vaatimukset".

## ⏭️ Vaihe 2 — Sensorien ryhmittely ja Dashboard (seuraavaksi)

Ideana siirtyä raakalistasta selkeään näkymään (määrittelyn luvut 24–25):

- [ ] Poimi tärkeimmät arvot: CPU käyttö/lämpö/kellot, GPU käyttö/lämpö/hotspot,
      RAM %, levyjen lämmöt, tuulettimet
- [ ] Dashboard-kortit (CPU / GPU / RAM / Levyt / Tuulettimet)
- [ ] Erottele sensorityypit siisteihin ryhmiin

## ⏭️ Vaihe 3 — Lokitus (SQLite)

- [ ] SQLite-tietokanta sensorihistorialle (luku 21)
- [ ] Sensoririvit 5 s välein (luku 14)
- [ ] Tapahtumaloki JSONL/SQLite (luku 15)

## ⏭️ Vaihe 4 — Raja-arvot ja varoitukset

- [ ] Oletusrajat (luku 16) + JSON-asetukset (luku 29)
- [ ] Kesto huomioon (piikki vs. pitkäkestoinen ongelma)
- [ ] Varoitus/kriittinen-tilat UI:hin

## ⏭️ Vaihe 5 — Windows Event Log

- [ ] Kernel-Power 41, WHEA-Logger, näyttöajurivirheet, levy/NTFS/NVMe, BugCheck
      (`System.Diagnostics.Eventing.Reader`, luvut 18 ja 32)

## ⏭️ Vaihe 6 — Riskianalyysi

- [ ] Pisteytys ja selkokielinen yhteenveto (luvut 19 ja 31)
- [ ] "Ennen kaatumista" -puskuri + `last_state.json` (luku 17)

## ⏭️ Vaihe 7 — Raportointi

- [ ] "Luo raportti" Markdown/TXT (luku 20), CSV-vienti

## ⏭️ Vaihe 8 — Viimeistely

- [ ] Tray icon, autostart, ilmoitukset, asetussivu, graafit (LiveCharts2)

---

## Tekninen pino (luku 22)

| Osa | Valinta |
|---|---|
| Kieli | C# |
| UI | WPF (net8.0-windows) |
| Sensorit | LibreHardwareMonitorLib 0.9.6 + PawnIO-ajuri |
| Event Log | System.Diagnostics.Eventing.Reader |
| Tietokanta | SQLite (tulossa vaiheessa 3) |
| Graafit | LiveCharts2 (tulossa vaiheessa 8) |

## MVP:n ulkopuolella (luku 24)

Tuulettimien ohjaus, RGB, ylikellotus, jännitteiden muuttaminen, automaattinen säätö,
overlay, pilvisynkronointi. **Ensimmäinen versio vain lukee, näyttää, lokittaa ja analysoi.**
