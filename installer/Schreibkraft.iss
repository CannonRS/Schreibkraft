#define AppName "Schreibkraft"
#ifndef AppVersion
#define AppVersion "1.3.0"
#endif
#ifndef SourceDir
#define SourceDir "..\artifacts\publish\Schreibkraft"
#endif
#ifndef OutputDir
#define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{E8071889-89D2-4D1C-A4AA-2408C56D9D5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Ronny Schulz
AppPublisherURL=https://github.com/CannonRS/Schreibkraft
AppSupportURL=https://github.com/CannonRS/Schreibkraft/issues
AppUpdatesURL=https://github.com/CannonRS/Schreibkraft/releases
DefaultDirName={localappdata}\Programs\Schreibkraft
DefaultGroupName=Schreibkraft
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Schreibkraft-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
SetupIconFile=..\src\Schreibkraft\Assets\App.ico
UninstallDisplayIcon={app}\Schreibkraft.exe
CloseApplications=yes
CloseApplicationsFilter=Schreibkraft.exe
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany=Ronny Schulz
VersionInfoDescription=Schreibkraft Installer
VersionInfoProductName=Schreibkraft
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Verknüpfungen:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Prerequisites.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\Schreibkraft"; Filename: "{app}\Schreibkraft.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Schreibkraft.exe"
Name: "{autodesktop}\Schreibkraft"; Filename: "{app}\Schreibkraft.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Schreibkraft.exe"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{tmp}\Prerequisites.ps1"" -DotNetDesktopRuntimeVersion ""10.0.7"""; StatusMsg: "Voraussetzungen werden geprüft ..."; Flags: runhidden waituntilterminated
Filename: "{app}\Schreibkraft.exe"; Description: "Schreibkraft starten"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Get-Process -Name 'Schreibkraft' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "StopSchreibkraft"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Schreibkraft' -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "RemoveSchreibkraftAutostart"
