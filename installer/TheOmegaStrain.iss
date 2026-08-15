#define MyAppName "The Omega Strain"
#define MyAppExeName "TheOmegaStrain.exe"
#define MyAppPublisher "RetroMesh"
#define MyAppId "{{73E9B899-CA3D-4F78-A8B3-B2A912F238D3}"
#define InstallerMusicFile "InstallerMusic.wav"

#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#ifndef PublishDir
#error PublishDir must be supplied by Build-Installer.ps1
#endif

#ifndef OutputDir
#error OutputDir must be supplied by Build-Installer.ps1
#endif

#ifndef SecretsSource
#error SecretsSource must be supplied by Build-Installer.ps1
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\The Omega Strain
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=TheOmegaStrainSetup-{#AppVersion}
SetupIconFile=..\TheOmegaStrain.Wpf\GameGraphics\OmegaStrain.ico
WizardImageFile=Assets\InstallerWizardImage.bmp
WizardSmallImageFile=Assets\InstallerWizardSmall.bmp
WizardBackImageFile=Assets\InstallerWizardBackImage.bmp
WizardBackImageOpacity=255
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=150
WizardImageStretch=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Create a desktop launch shortcut"; GroupDescription: "Flight deck options:"

[Dirs]
Name: "{userappdata}\OmegaStrain"; Flags: uninsneveruninstall

[Files]
Source: "..\TheOmegaStrain.Wpf\Soundeffects\Spekeord_intro.wav"; DestName: "{#InstallerMusicFile}"; Flags: dontcopy nocompression
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs solidbreak
Source: "{#SecretsSource}"; DestDir: "{userappdata}\OmegaStrain"; DestName: "secrets.json"; Flags: ignoreversion onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Asterion systems"; Flags: nowait postinstall skipifsilent

[LangOptions]
DialogFontName=Segoe UI Semibold
DialogFontSize=11
WelcomeFontName=Segoe UI Semibold
WelcomeFontSize=20

[Messages]
SetupAppTitle=Asterion Deployment - {#MyAppName}
SetupWindowTitle=Asterion Deployment - {#MyAppName}
WelcomeLabel1=Welcome aboard Asterion
WelcomeLabel2=This wizard will deploy {#MyAppName} into your private ship systems.%n%nNavigation data, audio systems, combat routines and launch files will be staged for your next Omega Strain run.
SelectDirDesc=Choose the Asterion systems bay
SelectDirLabel3=Setup will deploy {#MyAppName} into the following ship systems folder.
SelectDirBrowseLabel=To deploy to this bay, choose Continue. To select another systems bay, choose Browse.
ReadyLabel1=Asterion is ready to receive the deployment.
ReadyLabel2a=Choose Deploy to stage the software package. Choose Back to review the flight plan.
ReadyLabel2b=Choose Deploy to stage the software package.
InstallingLabel=Deploying {#MyAppName} to Asterion's systems...
FinishedHeadingLabel=Deployment complete
FinishedLabel=Asterion has received {#MyAppName}.%n%nChoose Launch when you are ready to enter the cockpit.
FinishedLabelNoIcons=Asterion has received {#MyAppName}.%n%nChoose Launch when you are ready to enter the cockpit.
ExitSetupMessage=Abort Asterion deployment? No ship systems will be changed.
ButtonNext=&Continue
ButtonInstall=&Deploy
ButtonFinish=&Launch
ButtonCancel=&Abort

[Code]
const
  SND_ASYNC = $0001;
  SND_NODEFAULT = $0002;
  SND_LOOP = $0008;
  SND_FILENAME = $00020000;

function PlaySound(pszSound: String; hmod: Integer; fdwSound: Integer): Boolean;
  external 'PlaySoundW@winmm.dll stdcall';

procedure StartInstallerMusic();
var
  MusicPath: String;
begin
  if WizardSilent() then
  begin
    Exit;
  end;

  ExtractTemporaryFile('{#InstallerMusicFile}');
  MusicPath := ExpandConstant('{tmp}\{#InstallerMusicFile}');
  PlaySound(MusicPath, 0, SND_FILENAME or SND_ASYNC or SND_LOOP or SND_NODEFAULT);
end;

procedure StopInstallerMusic();
begin
  PlaySound('', 0, 0);
end;

function InitializeSetup(): Boolean;
begin
  StartInstallerMusic();
  Result := True;
end;

procedure InitializeWizard();
begin
  { The full-page cockpit art is pre-faded into an opaque BMP. Hide the stock
    wizard images so Inno does not repaint grey/white image panels over it on
    some DPI/theme combinations. }
  WizardForm.WizardBitmapImage.Visible := False;
  WizardForm.WizardBitmapImage2.Visible := False;
  WizardForm.WizardSmallBitmapImage.Visible := False;
end;

procedure DeinitializeSetup();
begin
  StopInstallerMusic();
end;
