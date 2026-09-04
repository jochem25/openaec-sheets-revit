; OpenAEC Sheet Exporter — per-user installer (geen admin vereist)
; Bouwen via build\Build-Installer.ps1 (die geeft AppVersion en PublishDir mee)

#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif
#ifndef PublishDir
  #define PublishDir "publish"
#endif

#define AppName "OpenAEC Sheet Exporter"
#define AddinFile "OpenAEC.Sheets.Revit.addin"
#define PluginFolder "OpenAEC.Sheets"

[Setup]
AppId={{7F3A1C2E-5EE7-4B0A-9D01-B2C3D4E50001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=OpenAEC Foundation
AppPublisherURL=https://open-aec.com
VersionInfoVersion={#AppVersion}
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=OpenAEC-SheetExporter-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Components]
Name: "revit2025"; Description: "Revit 2025"; Types: full
Name: "revit2026"; Description: "Revit 2026"; Types: full

[Files]
; Revit 2025
Source: "{#AddinFile}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Components: revit2025; Flags: ignoreversion
Source: "{#PublishDir}\*"; Excludes: "*.pdb"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginFolder}"; Components: revit2025; Flags: ignoreversion recursesubdirs
; Revit 2026
Source: "{#AddinFile}"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Components: revit2026; Flags: ignoreversion
Source: "{#PublishDir}\*"; Excludes: "*.pdb"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginFolder}"; Components: revit2026; Flags: ignoreversion recursesubdirs

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\{#PluginFolder}"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\{#PluginFolder}"

[Code]
function IsRevitRunning: Boolean;
var
  Locator, WMI, Procs: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    WMI := Locator.ConnectServer('.', 'root\cimv2');
    Procs := WMI.ExecQuery('SELECT Name FROM Win32_Process WHERE Name = ''Revit.exe''');
    Result := Procs.Count > 0;
  except
    // WMI niet beschikbaar -> niet blokkeren; in-use files geven dan alsnog een nette fout
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  while IsRevitRunning do
  begin
    if WizardSilent then
    begin
      Log('Revit draait — installatie afgebroken (silent mode).');
      Result := False;
      Exit;
    end;
    if MsgBox('Revit is nog geopend. Sluit Revit en klik op OK om verder te gaan.',
              mbError, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// Vink alleen Revit-versies aan die op deze machine gebruikt worden
// (Addins-map bestaat zodra Revit ooit gestart is)
procedure InitializeWizard;
var
  I: Integer;
  Caption: String;
begin
  for I := 0 to WizardForm.ComponentsList.Items.Count - 1 do
  begin
    Caption := WizardForm.ComponentsList.ItemCaption[I];
    if Caption = 'Revit 2025' then
      WizardForm.ComponentsList.Checked[I] :=
        DirExists(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\2025'));
    if Caption = 'Revit 2026' then
      WizardForm.ComponentsList.Checked[I] :=
        DirExists(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\2026'));
  end;
end;
