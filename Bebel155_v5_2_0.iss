#define MyAppName "Bebel Equipe Do Mais Novo 155"
#define MyAppVersion "5.2.0"
#define MyAppExeName "Bebel-155_V5_2_0.exe"

[Setup]
AppId={{A6E8A4F4-0E29-4E78-A9B4-155BEBE10012}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Bebel Equipe Do Mais Novo 155
DefaultDirName={autopf}\Bebel Equipe Do Mais Novo 155
DefaultGroupName=Bebel Equipe Do Mais Novo 155
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=Bebel-155_Setup_V5_2_0
SetupIconFile=Assets\bebel155.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=Assets\wizard_large.bmp
WizardSmallImageFile=Assets\wizard_small.bmp
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "build\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "COMPILAR_COM_DOTNET_MODERNO.ps1"; DestDir: "{app}\Ferramentas"; Flags: ignoreversion
Source: "REPARAR_WINGET_APP_INSTALLER.bat"; DestDir: "{app}\Ferramentas"; Flags: ignoreversion
Source: "REPARAR_WINGET_APP_INSTALLER.ps1"; DestDir: "{app}\Ferramentas"; Flags: ignoreversion
Source: "CONFIGURAR_DEPENDENCIAS_V5_1.bat"; DestDir: "{app}\Ferramentas"; Flags: ignoreversion skipifsourcedoesntexist
Source: "INSTALAR_DEPENDENCIAS_BEBEL155.ps1"; DestDir: "{app}\Ferramentas"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Atualizacao\COMO_CONFIGURAR_UPDATE.txt"; DestDir: "{app}\Atualizacao"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Atualizacao\manifest.example.json"; DestDir: "{app}\Atualizacao"; Flags: ignoreversion skipifsourcedoesntexist
Source: "Catalogs\android_devices.json"; DestDir: "{app}\Catalogs"; Flags: ignoreversion
Source: "Catalogs\apple_devices.json"; DestDir: "{app}\Catalogs"; Flags: ignoreversion
Source: "Catalogs\catalog-manifest.json"; DestDir: "{app}\Catalogs"; Flags: ignoreversion
Source: "Drivers\*"; DestDir: "{app}\Drivers"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Bebel Equipe Do Mais Novo 155"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Bebel Equipe Do Mais Novo 155"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Configurar dependencias"; Filename: "{app}\Ferramentas\CONFIGURAR_DEPENDENCIAS_V5_1.bat"; WorkingDir: "{app}\Ferramentas"
Name: "{group}\Reparar WinGet - App Installer"; Filename: "{app}\Ferramentas\REPARAR_WINGET_APP_INSTALLER.bat"; WorkingDir: "{app}\Ferramentas"
Name: "{group}\Desinstalar Bebel Equipe Do Mais Novo 155"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Bebel Equipe Do Mais Novo 155"; Flags: nowait postinstall skipifsilent
