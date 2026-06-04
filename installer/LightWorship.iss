#define AppName "LightWorship"
#define AppVersion "0.13.0"
#define AppPublisher "LightWorship"
#define SourceRoot ".."

[Setup]
AppId={{B41B8931-28E0-4A2E-90E1-70F6277596CC}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\LightWorship
DefaultGroupName=LightWorship
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=LightWorship-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceRoot}\src\LightWorship\bin\Release\LightWorship.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\src\LightWorship\bin\Release\LightWorship.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\src\LightWorship\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#SourceRoot}\data\Bibles\kjv-verses-1769.json"; DestDir: "{app}\data\Bibles"; Flags: ignoreversion

[Icons]
Name: "{group}\LightWorship"; Filename: "{app}\LightWorship.exe"
Name: "{autodesktop}\LightWorship"; Filename: "{app}\LightWorship.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LightWorship.exe"; Description: "{cm:LaunchProgram,LightWorship}"; Flags: nowait postinstall skipifsilent
