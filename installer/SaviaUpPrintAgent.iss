; Compile this script through scripts/New-Installer.ps1.
; The build script supplies the self-contained publish directory and version.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  #error SourceDir must point to the published Savia Up Print Agent files.
#endif

[Setup]
AppId={{F0B1C54C-CB24-4C30-9D9D-D582E2D67A78}
AppName=Savia Up Print Agent
AppVersion={#AppVersion}
AppPublisher=Savia Up
SetupIconFile=assets\SaviaUp.ico
WizardSmallImageFile=assets\SaviaUpWizardSmall.bmp
DefaultDirName={autopf}\SaviaUp\PrintAgent
DefaultGroupName=Savia Up
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=SaviaUpPrintAgent-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=Savia Up Print Agent
UninstallDisplayIcon={app}\SaviaUp.ico
CloseApplications=yes

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "assets\SaviaUp.ico"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{sys}\sc.exe"; Parameters: "create ""SaviaUpPrintAgent"" binPath= """"""{app}\SaviaUp.PrintAgent.exe"""""" start= auto DisplayName= ""Savia Up Print Agent"""; Flags: runhidden waituntilterminated; Check: not ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Instalando el servicio de impresión..."
Filename: "{sys}\sc.exe"; Parameters: "config ""SaviaUpPrintAgent"" binPath= """"""{app}\SaviaUp.PrintAgent.exe"""""" start= auto DisplayName= ""Savia Up Print Agent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Actualizando el servicio de impresión..."
Filename: "{sys}\sc.exe"; Parameters: "failure ""SaviaUpPrintAgent"" reset= 86400 actions= restart/5000/restart/15000/restart/60000"; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Configurando recuperación automática..."
Filename: "{sys}\sc.exe"; Parameters: "start ""SaviaUpPrintAgent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Iniciando el agente de impresión..."

[Code]
const
  PrintAgentServiceName = 'SaviaUpPrintAgent';
  PrintAgentUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{F0B1C54C-CB24-4C30-9D9D-D582E2D67A78}_is1';

var
  ExistingInstallationPage: TInputOptionWizardPage;
  ExistingUninstaller: String;

function ServiceExists(const ServiceName: String): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + ServiceName + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;

function GetExistingUninstaller(): String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM64, PrintAgentUninstallKey, 'UninstallString', Result) then
    RegQueryStringValue(HKLM, PrintAgentUninstallKey, 'UninstallString', Result);
end;

function StopPrintAgentService(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not ServiceExists(PrintAgentServiceName) then
    Exit;

  if not Exec(ExpandConstant('{sys}\sc.exe'), 'stop "' + PrintAgentServiceName + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) then
  begin
    Result := False;
    Exit;
  end;

  { 1060: el servicio ya no existe; 1062: ya estaba detenido. }
  if (ResultCode <> 0) and (ResultCode <> 1060) and (ResultCode <> 1062) then
  begin
    Result := False;
    Exit;
  end;

  { El Worker responde al stop ordenado. Evita copiar binarios mientras continúa activo. }
  Sleep(3000);
end;

function DeletePrintAgentService(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not ServiceExists(PrintAgentServiceName) then
    Exit;

  if not Exec(ExpandConstant('{sys}\sc.exe'), 'delete "' + PrintAgentServiceName + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) then
  begin
    Result := False;
    Exit;
  end;

  { 1060: no existe; 1072: quedó programado para eliminarse al cerrar el proceso. }
  Result := (ResultCode = 0) or (ResultCode = 1060) or (ResultCode = 1072);
end;

function SplitCommandLine(const CommandLine: String; var FileName, Parameters: String): Boolean;
var
  ClosingQuote: Integer;
  FirstSpace: Integer;
begin
  FileName := '';
  Parameters := '';
  if CommandLine = '' then
  begin
    Result := False;
    Exit;
  end;

  if CommandLine[1] = '"' then
  begin
    ClosingQuote := Pos('"', Copy(CommandLine, 2, MaxInt));
    if ClosingQuote = 0 then
    begin
      Result := False;
      Exit;
    end;
    FileName := Copy(CommandLine, 2, ClosingQuote - 1);
    Parameters := Trim(Copy(CommandLine, ClosingQuote + 2, MaxInt));
  end
  else
  begin
    FirstSpace := Pos(' ', CommandLine);
    if FirstSpace = 0 then
      FileName := CommandLine
    else
    begin
      FileName := Copy(CommandLine, 1, FirstSpace - 1);
      Parameters := Trim(Copy(CommandLine, FirstSpace + 1, MaxInt));
    end;
  end;
  Result := FileName <> '';
end;

function RunExistingUninstaller(): Boolean;
var
  FileName: String;
  Parameters: String;
  ResultCode: Integer;
begin
  Result := False;
  if not SplitCommandLine(ExistingUninstaller, FileName, Parameters) then
    Exit;

  Result := Exec(FileName, Parameters, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

procedure InitializeWizard();
begin
  ExistingUninstaller := GetExistingUninstaller();
  if ExistingUninstaller = '' then
    Exit;

  ExistingInstallationPage := CreateInputOptionPage(wpWelcome,
    'El agente ya está instalado',
    'Selecciona la acción que deseas realizar',
    'Actualizar conserva la configuración local, la cola de impresión y la vinculación actual. ' +
    'Desinstalar abre el desinstalador de Windows y cierra este asistente.',
    True, False);
  ExistingInstallationPage.Add('Actualizar el agente de impresión');
  ExistingInstallationPage.Add('Desinstalar el agente de impresión');
  ExistingInstallationPage.SelectedValueIndex := 0;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (ExistingInstallationPage = nil) or (CurPageID <> ExistingInstallationPage.ID)
    or (ExistingInstallationPage.SelectedValueIndex <> 1) then
    Exit;

  Result := False;
  if MsgBox('Se abrirá el desinstalador de Savia Up Print Agent. ¿Deseas continuar?',
    mbConfirmation, MB_OKCANCEL) <> IDOK then
    Exit;

  if not RunExistingUninstaller() then
  begin
    MsgBox('No fue posible completar la desinstalación. Ejecuta este instalador como administrador e inténtalo de nuevo.',
      mbError, MB_OK);
    Exit;
  end;

  MsgBox('El agente se desinstaló correctamente. Cierra este asistente.', mbInformation, MB_OK);
  WizardForm.Close;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopPrintAgentService() then
    Result := 'No fue posible detener el servicio SaviaUpPrintAgent. Cierra cualquier proceso del agente e inténtalo de nuevo.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  if not StopPrintAgentService() then
    Log('No fue posible detener el servicio SaviaUpPrintAgent durante la desinstalación.');
  if not DeletePrintAgentService() then
    MsgBox('No fue posible eliminar el servicio SaviaUpPrintAgent. Reinicia Windows y vuelve a ejecutar la desinstalación como administrador.',
      mbError, MB_OK);
end;
