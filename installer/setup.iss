; Hardware Monitorin asennusohjelma (Inno Setup 6).
;
; Rakennus: käytä AINA .\tools\release.ps1 -skriptiä. Se tyhjentää publishin,
; julkaisee, tarkistaa versiot ja lisenssit, allekirjoittaa exet ja kääntää
; tämän. Älä aja dotnet publishia käsin olemassa olevaan publish-hakemistoon —
; vanhentuneet tiedostot päätyisivät alla olevan [Files]-wildcardin kautta
; asennukseen.
;
; Tuloste: installer\Output\HardwareMonitor-Setup-<versio>.exe
;
; Asennus Program Filesiin on samalla turvasäännön edellytys: autostart-
; tehtävä saa käynnistää sovelluksen korotettuna vain ACL-suojatusta polusta.
; Käyttäjän data (%LOCALAPPDATA%\HardwareMonitor) säilyy poistossa.

#define MyAppName "Hardware Monitor"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "jrs8205"
#define MyAppURL "https://github.com/jrs8205/Computer-App"
#define MyAppExeName "HardwareMonitor.exe"

[Setup]
; AppId yksilöi sovelluksen päivityksiä ja poistoa varten — ÄLÄ muuta.
AppId={{B7E5D9A4-3C61-4E8F-9B2A-7F4D8C1E5A90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
; Sovellus vaatii adminin (sensorit) ja asentuu Program Filesiin.
PrivilegesRequired=admin
; Ajossa oleva sovellus suljetaan Restart Managerilla — todennettu, että
; sulkeminen on SIISTI (CleanShutdown säilyy, ei kaatumismerkintää).
; EI AppMutexia: sen tarkistus keskeyttäisi hiljaisen asennuksen ennen
; kuin Restart Manager ehtii sulkea sovelluksen.
CloseApplications=yes
RestartApplications=no
OutputDir=Output
OutputBaseFilename=HardwareMonitor-Setup-{#MyAppVersion}
SetupIconFile=..\src\HardwareMonitor.App\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "finnish"; MessagesFile: "compiler:Languages\Finnish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; \
    GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; \
    Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Poista autostart-tehtävä, jonka sovellus on voinut luoda (asetus
; "Käynnistä Windowsin mukana"). /F ohittaa vahvistuksen; puuttuva
; tehtävä ei kaada poistoa.
Filename: "schtasks.exe"; Parameters: "/Delete /F /TN HardwareMonitor"; \
    Flags: runhidden; RunOnceId: "RemoveAutostartTask"
