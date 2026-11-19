; Instalador de Capataz (F9-03).
; ASCII puro (regla 0.19 del plan F9). Lo compila scripts\construir-instalador.ps1, que pasa:
;   /DVersionApp=x.y.z   /DOrigen=<carpeta de publicacion>   /DSalida=<carpeta del instalador>

#if Ver < EncodeVer(6, 3, 0)
  #error Se necesita Inno Setup 6.3 o superior (x64compatible)
#endif
#ifndef VersionApp
  #error Falta /DVersionApp: use scripts\construir-instalador.ps1
#endif
#ifndef Origen
  #error Falta /DOrigen: use scripts\construir-instalador.ps1
#endif
#ifndef Salida
  #error Falta /DSalida: use scripts\construir-instalador.ps1
#endif

#define NombreApp "Capataz"
#define Editor "Pesalab"
#define Exe "PesajeFaeneo.WinForms.exe"
#define CarpetaDatos "{commonappdata}\PesajeFaeneo"

[Setup]
; AppId FIJO PARA SIEMPRE (D9): cambiarlo haria que una version nueva se instale al lado de la anterior.
AppId={{0157C544-528F-4307-A487-5208BF10696F}
AppName={#NombreApp}
AppVersion={#VersionApp}
AppVerName={#NombreApp} {#VersionApp}
AppPublisher={#Editor}
VersionInfoVersion={#VersionApp}
VersionInfoProductName={#NombreApp}
DefaultDirName={autopf}\PesajeFaeneo
UsePreviousAppDir=yes
DefaultGroupName={#NombreApp}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; No reemplazar archivos con el sistema abierto (D10); no cerrarlo a la fuerza durante su backup de cierre.
AppMutex=Global\PesajeFaeneo
CloseApplications=no
OutputDir={#Salida}
OutputBaseFilename=Capataz-Setup-{#VersionApp}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#NombreApp}
UninstallDisplayIcon={app}\{#Exe}
; Marca del producto (logo C con pesa); los genera scripts\generar-marca.py. Rutas relativas a este .iss.
SetupIconFile=..\src\PesajeFaeneo.WinForms\Recursos\app.ico
WizardSmallImageFile=instalador-pequeno.bmp
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "escritorio"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; Datos compartidos por todos los usuarios de Windows del equipo (D11). Nunca se borran al desinstalar.
Name: "{#CarpetaDatos}"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#CarpetaDatos}\config"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#CarpetaDatos}\logs"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#CarpetaDatos}\logo"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#CarpetaDatos}\backups"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#CarpetaDatos}\restore"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "{#Origen}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENCIAS-TERCEROS.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#NombreApp}"; Filename: "{app}\{#Exe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#NombreApp}"; Filename: "{app}\{#Exe}"; WorkingDir: "{app}"; Tasks: escritorio

[Run]
; Corrige tambien archivos creados por versiones anteriores. SID de BUILTIN\Users: el nombre cambia con el idioma (H8).
Filename: "{sys}\icacls.exe"; Parameters: """{#CarpetaDatos}"" /grant *S-1-5-32-545:(OI)(CI)M /T /C /Q"; Flags: runhidden waituntilterminated; StatusMsg: "Configurando permisos de la carpeta de datos..."
; Primera ejecucion como el usuario que instalo, nunca elevada (H14).
Filename: "{app}\{#Exe}"; Description: "{cm:LaunchProgram,{#NombreApp}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
    MsgBox('Los datos del sistema (base de datos, copias de seguridad y configuracion) se conservaron en ' +
      ExpandConstant('{#CarpetaDatos}') + '. Borrelos manualmente solo si ya no los necesita.', mbInformation, MB_OK);
end;
