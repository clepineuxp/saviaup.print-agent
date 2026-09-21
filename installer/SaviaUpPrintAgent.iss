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

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{sys}\sc.exe"; Parameters: "stop ""SaviaUpPrintAgent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Actualizando el servicio de impresión..."
Filename: "{sys}\sc.exe"; Parameters: "create ""SaviaUpPrintAgent"" binPath= """"""{app}\SaviaUp.PrintAgent.exe"""""" start= auto DisplayName= ""Savia Up Print Agent"""; Flags: runhidden waituntilterminated; Check: not ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Instalando el servicio de impresión..."
Filename: "{sys}\sc.exe"; Parameters: "config ""SaviaUpPrintAgent"" binPath= """"""{app}\SaviaUp.PrintAgent.exe"""""" start= auto DisplayName= ""Savia Up Print Agent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); StatusMsg: "Actualizando el servicio de impresión..."
Filename: "{sys}\sc.exe"; Parameters: "failure ""SaviaUpPrintAgent"" reset= 86400 actions= restart/5000/restart/15000/restart/60000"; Flags: runhidden waituntilterminated; StatusMsg: "Configurando recuperación automática..."
Filename: "{sys}\sc.exe"; Parameters: "start ""SaviaUpPrintAgent"""; Flags: runhidden waituntilterminated; StatusMsg: "Iniciando el agente de impresión..."

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ""SaviaUpPrintAgent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); RunOnceId: "StopSaviaUpPrintAgent"
Filename: "{sys}\sc.exe"; Parameters: "delete ""SaviaUpPrintAgent"""; Flags: runhidden waituntilterminated; Check: ServiceExists('SaviaUpPrintAgent'); RunOnceId: "DeleteSaviaUpPrintAgent"

[Code]
function ServiceExists(const ServiceName: String): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'query "' + ServiceName + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  Result := ResultCode = 0;
end;
