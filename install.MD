# Instalación de Savia Up Print Agent

Esta versión deja preparado el agente como Worker/Windows Service. El instalador gráfico definitivo no forma parte de esta entrega.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Acceso HTTPS/WSS al backend de Savia Up.
- Impresoras USB instaladas en Windows o impresoras ESC/POS accesibles por IP.
- Una cuenta de Windows con permiso para instalar servicios.
- Para publicación dependiente del framework: .NET 10 Runtime. La alternativa self-contained no lo requiere.

La cuenta del servicio debe poder usar las colas del Windows Print Spooler y escribir en `%ProgramData%\SaviaUp\PrintAgent`. Para impresoras de red debe poder abrir TCP al puerto configurado (normalmente 9100).

## Publicación

Desde la raíz del repositorio:

```powershell
dotnet restore saviaup.print-agent.sln
dotnet test saviaup.print-agent.sln -c Release
dotnet publish saviaup.print-agent.Worker\saviaup.print-agent.Worker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\artifacts\win-x64
```

`--self-contained true` facilita la instalación en restaurantes porque incluye el runtime. Aumenta el tamaño del paquete. Para equipos administrados que ya tengan .NET 10 puede usarse `--self-contained false`.

Antes de distribuir, firme los binarios y publique el paquete por HTTPS. No incluya un token o código de pairing dentro del artefacto.

## Configuración

Copie el contenido publicado a una ruta estable, por ejemplo `C:\Program Files\SaviaUp\PrintAgent`. Edite `appsettings.json` o agregue `appsettings.Production.json`:

```json
{
  "PrintAgent": {
    "BackendUrl": "https://api.saviaup.example",
    "DataDirectory": "%ProgramData%\\SaviaUp\\PrintAgent",
    "PollingIntervalSeconds": 30,
    "HeartbeatIntervalSeconds": 20,
    "MaximumAttempts": 10,
    "InitialRetryDelaySeconds": 5,
    "MaximumRetryDelaySeconds": 300
  }
}
```

Al arrancar, el agente aparece automáticamente en la sección Impresión de Savia Up. Un administrador selecciona el equipo y lo vincula a la sede. No se escribe ningún código ni token en `appsettings`. Una vez vinculado:

- el backend asocia de forma inmutable la credencial con organización, sede y agente;
- `device-token.dat` contiene el token cifrado con DPAPI para la máquina local;
- nunca copie `device-token.dat` a otro computador.

`AgentId`, `TenantId` y `LocationId` no se configuran manualmente: provienen del pairing. No coloque secretos reales en archivos versionados.

## Instalación como Windows Service

Abra PowerShell como administrador. Verifique primero la ruta absoluta del ejecutable:

```powershell
$agentExe = 'C:\Program Files\SaviaUp\PrintAgent\SaviaUp.PrintAgent.exe'
Test-Path -LiteralPath $agentExe
```

Registre el servicio con inicio automático:

```powershell
sc.exe create "SaviaUpPrintAgent" binPath= "`"C:\Program Files\SaviaUp\PrintAgent\SaviaUp.PrintAgent.exe`"" start= auto DisplayName= "Savia Up Print Agent"
sc.exe description "SaviaUpPrintAgent" "Impresión automática de comandas Savia Up"
sc.exe failure "SaviaUpPrintAgent" reset= 86400 actions= restart/5000/restart/15000/restart/60000
sc.exe start "SaviaUpPrintAgent"
```

Compruebe el estado:

```powershell
sc.exe query "SaviaUpPrintAgent"
Get-Content -Tail 100 'C:\ProgramData\SaviaUp\PrintAgent\logs\agent-*.log'
```

Después del primer arranque, confirme en Savia Up que el agente aparece `Online` y que las impresoras instaladas fueron detectadas. Cree las zonas y ejecute una impresión de prueba antes de habilitarlo en producción.

## Datos locales y permisos

Por defecto:

```text
C:\ProgramData\SaviaUp\PrintAgent\print-queue.db
C:\ProgramData\SaviaUp\PrintAgent\device-token.dat
C:\ProgramData\SaviaUp\PrintAgent\logs\
```

Restrinja ACL al grupo Administradores y a la cuenta del servicio. Haga backup del SQLite solo con el servicio detenido. El token no debe salir del equipo.

## Actualización manual

1. Publique y firme la nueva versión.
2. Ejecute `sc.exe stop "SaviaUpPrintAgent"`.
3. Haga copia recuperable del directorio del programa y de `print-queue.db`.
4. Reemplace únicamente los binarios/configuración versionable; conserve `%ProgramData%\SaviaUp\PrintAgent`.
5. Ejecute `sc.exe start "SaviaUpPrintAgent"` y valide logs, heartbeat y prueba de impresión.

Una estrategia futura de auto-update debe usar paquetes firmados, manifiesto con hash, descarga atómica y rollback. No está implementada en esta fase.

## Desinstalación

```powershell
sc.exe stop "SaviaUpPrintAgent"
sc.exe delete "SaviaUpPrintAgent"
```

Después puede quitar el directorio de `Program Files`. Conserve temporalmente `%ProgramData%\SaviaUp\PrintAgent` si necesita auditoría o recuperación de cola. Eliminarlo borra la cola, logs y credencial local; desvincule además el agente desde Savia Up para revocar el token del servidor.

## Instalador futuro recomendado

Se recomienda **WiX Toolset** para producir `SaviaUp.PrintAgent.Setup.exe`/MSI porque permite instalar y actualizar un Windows Service, configurar recuperación, ACL, firma y upgrades mayores de forma reproducible. Inno Setup es más simple para un prototipo, pero requiere scripts adicionales para un ciclo de vida empresarial del servicio. MSIX ofrece buen aislamiento, aunque los Windows Services y escenarios de instalación fuera de Store son menos directos.

La experiencia objetivo del instalador WiX será: descargar, aceptar términos, elegir URL/ubicación, instalar el servicio y verificar conectividad. Al abrir Savia Up, el administrador verá el equipo disponible y podrá vincularlo; el instalador nunca incluirá credenciales permanentes.
