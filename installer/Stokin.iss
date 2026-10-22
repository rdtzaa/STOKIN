#define AppVersion "0.1.0"
[Setup]
AppId={{6106E152-9C5F-49CE-AF8C-BE1B9B51DA81}
AppName=STOKIN
AppVersion={#AppVersion}
AppPublisher=STOKIN
DefaultDirName={localappdata}\Programs\STOKIN
DefaultGroupName=STOKIN
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\output\installer
OutputBaseFilename=STOKIN-Setup-{#AppVersion}-x64
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Stokin.Desktop.exe
[Files]
Source: "..\output\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\STOKIN"; Filename: "{app}\Stokin.Desktop.exe"
Name: "{autodesktop}\STOKIN"; Filename: "{app}\Stokin.Desktop.exe"
[Run]
Filename: "{app}\Stokin.Desktop.exe"; Description: "Buka STOKIN dan konfigurasi koneksi SQL Server"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var Versions: TArrayOfString; I: Integer; Found: Boolean;
begin
  Found := False;
  if RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions)-1 do
      if Copy(Versions[I],1,2) = '8.' then Found := True;
  if not Found then
    MsgBox('Pasang .NET 8 Windows Desktop Runtime x64 sebelum menjalankan paket ini. SQL Server Express dan koneksi toko juga harus tersedia.', mbError, MB_OK);
  Result := Found;
end;
