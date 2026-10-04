#define AppVersion "0.1.1"
#ifndef PublishDir
  #define PublishDir "..\artifacts\release\publish"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\artifacts\release"
#endif
[Setup]
AppId={{75AF53B9-2BC6-4AC3-A7D4-859884B5EAF0}
AppName=ScreenIt
AppVersion={#AppVersion}
AppPublisher=nickkho0201
AppPublisherURL=https://github.com/nickkho0201/ScreenIt
AppSupportURL=https://github.com/nickkho0201/ScreenIt/issues
AppUpdatesURL=https://github.com/nickkho0201/ScreenIt/releases
DefaultDirName={localappdata}\Programs\ScreenIt
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
DisableProgramGroupPage=yes
DefaultGroupName=ScreenIt
LicenseFile=..\LICENSE
SetupIconFile=..\assets\ScreenIt.ico
UninstallDisplayIcon={app}\ScreenIt.App.exe
OutputDir={#ReleaseDir}
OutputBaseFilename=ScreenIt-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\ScreenIt.MVP
CloseApplications=no
RestartApplications=no
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{userprograms}\ScreenIt"; Filename: "{app}\ScreenIt.App.exe"; WorkingDir: "{app}"
[Run]
Filename: "{app}\ScreenIt.App.exe"; Description: "Launch ScreenIt"; Flags: nowait postinstall skipifsilent unchecked
; No autostart, desktop shortcut, or removal of user settings / clipboard generations.
