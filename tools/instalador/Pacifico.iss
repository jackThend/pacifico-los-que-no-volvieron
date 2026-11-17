; Instalador de Windows de «Pacífico: Los que no volvieron» (Inno Setup 6).
; No se compila a mano: lo lanza tools/instalador/crear_instalador.py, que pasa la versión (/DVersion=...) leída de
; GameBuilder.Version y prepara el archivo de créditos. Toma el juego ya compilado de
; src/UnityProject/Builds/StandaloneWindows64 (GameBuilder.BuildWindowsPlayerOnlyBatch).

#ifndef Version
  #define Version "0.0.0"
#endif
#define Nombre "Pacífico: Los que no volvieron"
; Nombre para carpetas y accesos directos: Windows no admite «:» en nombres de archivo.
#define Acceso "Pacífico - Los que no volvieron"
#define Juego "..\..\src\UnityProject\Builds\StandaloneWindows64"

[Setup]
; Identificador fijo: no cambiarlo, así una versión nueva se instala encima de la anterior.
AppId={{6E4C2B7A-1879-4E0F-9C1A-5A1F0C8D1884}
AppName={#Nombre}
AppVersion={#Version}
AppVerName={#Nombre} {#Version}
AppPublisher=Proyecto Pacífico
VersionInfoVersion={#Version}
VersionInfoDescription=Instalador de {#Nombre}
DefaultDirName={autopf}\Pacifico
DefaultGroupName={#Acceso}
DisableProgramGroupPage=yes
; Página de bienvenida con la franja del medallón (Inno Setup 6 la oculta si no se pide).
DisableWelcomePage=no
DisableDirPage=auto
; Se instala solo para el usuario, sin pedir permiso de administrador.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=salida
OutputBaseFilename=Pacifico_Instalador_{#Version}
SetupIconFile=recursos\Pacifico.ico
UninstallDisplayIcon={app}\Pacifico.exe
UninstallDisplayName={#Nombre}
WizardStyle=modern
WizardImageFile=recursos\Asistente_100.bmp,recursos\Asistente_125.bmp,recursos\Asistente_150.bmp,recursos\Asistente_175.bmp,recursos\Asistente_200.bmp
WizardSmallImageFile=recursos\Asistente_chico_100.bmp,recursos\Asistente_chico_125.bmp,recursos\Asistente_chico_150.bmp,recursos\Asistente_chico_175.bmp,recursos\Asistente_chico_200.bmp
WizardImageStretch=no
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ShowLanguageDialog=no
CloseApplications=yes

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
es.WelcomeLabel2=Se instalará [name/ver] en este equipo.%n%nUna historia de la Guerra del Pacífico (1879–1884) contada por los soldados y marineros de ambos bandos.%n%nEsta es una versión de prueba: gracias por jugarla y por contarnos qué te pareció.

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#Juego}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*_BurstDebugInformation_DoNotShip,*_BackUpThisFolder_ButDontShipItWithYourGame"
Source: "salida\preparado\Creditos.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#Acceso}"; Filename: "{app}\Pacifico.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\Créditos de {#Acceso}"; Filename: "{app}\Creditos.txt"
Name: "{autodesktop}\{#Acceso}"; Filename: "{app}\Pacifico.exe"; WorkingDir: "{app}"; Tasks: escritorio

[Run]
Filename: "{app}\Pacifico.exe"; Description: "Jugar ahora"; Flags: nowait postinstall skipifsilent
