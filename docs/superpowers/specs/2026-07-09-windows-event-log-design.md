# Vaihe 5 — Windows Event Log -valvonta (design)

Päivä: 9.7.2026. Perustuu määrittelyn lukuun 18 ja ROADMAPin Vaihe 5:een.

## Tavoite

Ohjelma lukee Windowsin System-lokista rautaan liittyvät tapahtumat
(odottamattomat sammutukset, rautavirheet, näyttöajurikaatumiset, levyvirheet,
BSOD:t) ja tallentaa ne samaan `events`-tauluun kuin raja-arvotapahtumat —
omalla aikaleimallaan, jolloin ne asettuvat sensorihistorian aikajanalle.
Vaihe 6 (riskianalyysi) käyttää näitä suoraan.

## Arkkitehtuuri (Core/WindowsEvents/)

```
WindowsLogEvent          DTO: Time, Provider, EventId, WindowsLevel (1–4), RecordId
WindowsEventClassifier   Puhdas logiikka: (provider, id, level) → luokitus tai null
IWindowsEventSource      Rajapinta: ReadSince(lastRecordId, maxAge)
SystemEventReader        EventLogReader-toteutus (ohut, ei yksikkötestata)
WindowsEventCollector    Orkestrointi: lue → luokittele → events-tauluun → bookmark
```

Bookmark = System-lokin `EventRecordID` (monotoninen per loki). Viimeksi
käsitelty id talletetaan HistoryDb:n uuteen `meta`-tauluun
(avain `windows_last_record_id`), jolloin uudelleenkäynnistys ei tuota
duplikaatteja. Ensimmäinen skannaus rajataan 30 päivään (= retention).

## Luokittelusäännöt

| Lähde | Ehto | Taso | Komponentti |
|---|---|---|---|
| Microsoft-Windows-Kernel-Power | id 41 | CRITICAL | Järjestelmä |
| EventLog | id 6008 (odottamaton sammutus) | WARNING | Järjestelmä |
| Microsoft-Windows-WER-SystemErrorReporting | id 1001 (BugCheck/BSOD) | CRITICAL | Järjestelmä |
| Microsoft-Windows-WHEA-Logger | Windows-taso Critical/Error | CRITICAL | Laitteisto |
| Microsoft-Windows-WHEA-Logger | muut (korjatut virheet) | WARNING | Laitteisto |
| Display / nvlddmkm | id 4101 tai Error-taso (TDR) | WARNING | GPU-ajuri |
| disk / Ntfs / storahci / stornvme | Windows-taso Critical/Error | CRITICAL | Levy |
| disk / Ntfs / storahci / stornvme | Windows-taso Warning | WARNING | Levy |
| kaikki muu | — | ohitetaan (null) | — |

events-tauluun: `ts` = tapahtuman oma aika, `sensor` = provider,
`value` = eventId, `message` = suomenkielinen selite.

## Ajoitus sovelluksessa

- Käynnistyksessä taustasäikeessä (ei viivytä UI:ta).
- Sen jälkeen 5 minuutin välein (1 s -päivitystimerin kylkeen, tick % 300).
- Virheet (esim. loki ei aukea) kirjataan debug.logiin, sovellus jatkaa.

## Rajaukset

- Vain System-loki. Application Error -tapahtumat (ohjelmien kaatumiset)
  jätetään pois: ROADMAP ei niitä Vaihe 5:een listaa ja ne olisivat meluisia.
- Ei vielä omaa UI-näkymää — tapahtumat näkyvät kannassa ja Vaihe 6/7
  tuo ne analyysiin ja raportteihin.
- Paketti: System.Diagnostics.EventLog 8.0.x (sisältää
  System.Diagnostics.Eventing.Reader -API:n .NET 8:lle).
