# Instalación de Savia Up Print Agent

El agente se distribuye mediante un instalador gráfico autocontenido para Windows x64. El
usuario acepta el aviso de administrador y el instalador registra e inicia el servicio; la
vinculación y la configuración de impresoras se realizan después desde Savia Up.

El instalador incluye el ícono y logo oficial de Savia Up en el ejecutable, asistente y entrada
de desinstalación de Windows.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Acceso HTTPS/WSS al backend de Savia Up.
- Impresoras USB instaladas en Windows o impresoras ESC/POS accesibles por IP.
- Una cuenta de Windows con permiso para instalar servicios.
- Para publicación dependiente del framework: .NET 10 Runtime. La alternativa self-contained no lo requiere.

La cuenta del servicio debe poder usar las colas del Windows Print Spooler y escribir en `%ProgramData%\SaviaUp\PrintAgent`. Para impresoras de red debe poder abrir TCP al puerto configurado (normalmente 9100).

## Generar el instalador para el VPS

En un equipo Windows de publicación instala el SDK de .NET 10 e Inno Setup 6. Desde la raíz
del repositorio ejecuta:

    .\scripts\New-Installer.ps1 -BackendUrl 'https://api.saviaup.com' -Version '1.0.0'

El script publica el agente autocontenido para win-x64, inserta la URL del backend en el
artefacto y genera ambos archivos:

    artifacts\installer\1.0.0\SaviaUpPrintAgent-Setup-1.0.0.exe
    artifacts\installer\1.0.0\SaviaUpPrintAgent-Setup.exe

Sube ese único archivo al VPS por HTTPS. En el backend configura la variable de entorno:

    Printing__AgentDownloadUrl=https://downloads.saviaup.com/SaviaUpPrintAgent-Setup.exe

La pantalla Impresión automática obtiene esa URL desde GET /api/printing/configuration y
muestra el botón de descarga.

## Publicación manual

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

## Instalación manual como Windows Service

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

## Actualización o desinstalación mediante el instalador

Al ejecutar una versión nueva del instalador en un computador que ya tiene el agente, se muestra
una decisión explícita:

- **Actualizar el agente de impresión**: detiene el servicio antes de reemplazar los binarios,
  conserva `%ProgramData%\SaviaUp\PrintAgent` y vuelve a registrar/iniciar el servicio.
- **Desinstalar el agente de impresión**: abre el desinstalador de Windows y cierra el instalador
  nuevo. Es equivalente a elegir *Savia Up Print Agent* desde *Aplicaciones instaladas*.

En cualquiera de las dos rutas de desinstalación, el proceso detiene y elimina
`SaviaUpPrintAgent` antes de retirar los archivos de la aplicación. La cola, logs y credencial de
`%ProgramData%\SaviaUp\PrintAgent` se conservan intencionalmente; elimínalos solo si ya no se
requiere recuperación ni auditoría y el agente fue desvinculado en Savia Up.

## Desinstalación

```powershell
sc.exe stop "SaviaUpPrintAgent"
sc.exe delete "SaviaUpPrintAgent"
```

También puede desinstalar desde *Configuración > Aplicaciones instaladas* de Windows. Ambas rutas
detienen y eliminan el servicio antes de quitar el programa. Conserve temporalmente
`%ProgramData%\SaviaUp\PrintAgent` si necesita auditoría o recuperación de cola. Eliminarlo borra
la cola, logs y credencial local; desvincule además el agente desde Savia Up para revocar el token
del servidor.

## Firma y distribución

El instalador Inno Setup automatiza la instalación, recuperación e inicio del servicio. Antes de
una distribución pública, firme el ejecutable y los binarios para reducir advertencias de
SmartScreen.
