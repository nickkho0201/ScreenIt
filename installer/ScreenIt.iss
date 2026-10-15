#define AppVersion "0.2.1"
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
; No desktop shortcut or removal of user settings / clipboard generations.
; The optional per-user autostart entry is removed only when it belongs to this installation.
[Code]
function NormalizedStartupPath(Value: String): String;
begin
  Result := Trim(Value);
  if (Length(Result) >= 2) and (Result[1] = '"') and (Result[Length(Result)] = '"') then
    Result := Trim(Copy(Result, 2, Length(Result) - 2));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StoredPath: String;
  InstalledPath: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    InstalledPath := ExpandConstant('{app}\ScreenIt.App.exe');
    // Revalidate the exact registered command immediately before deleting, and never touch a value that
    // belongs to another copy or application. Inno has no atomic compare-and-delete, so a registration
    // replaced in the gap between this query and the delete cannot be caught here.
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ScreenIt', StoredPath)
      and (CompareText(NormalizedStartupPath(StoredPath), InstalledPath) = 0) then
    begin
      // Delete the approval record only after the matching Run value was actually removed.
      if RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ScreenIt') then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', 'ScreenIt');
    end;
  end;
end;
