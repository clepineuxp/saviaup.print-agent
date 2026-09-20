# Savia Up Print Agent

Servicio local de impresión automática para Savia Up. Se ejecuta en segundo plano en Windows, recibe notificaciones del backend, descarga trabajos persistidos, los conserva en SQLite y los envía a impresoras instaladas en Windows o ESC/POS por red.

## Solución

```text
saviaup.print-agent.Shared
saviaup.print-agent.Domain
saviaup.print-agent.Core
saviaup.print-agent.Infrastructure
saviaup.print-agent.Worker
tests/saviaup.print-agent.Core.Tests
tests/saviaup.print-agent.IntegrationTests
```

La separación sigue las convenciones hexagonales de `saviaup.backend`: Domain declara contratos y puertos, Core contiene el procesamiento, Infrastructure implementa HTTP/SignalR, SQLite, DPAPI y drivers, y Worker compone el host.

## Desarrollo local

Requisitos: .NET SDK 10 y Windows 10/11. Configura únicamente `PrintAgent:BackendUrl` con la URL común del backend. No se requiere código ni token en `appsettings`; el agente queda disponible para vincularse desde Savia Up.

```powershell
dotnet restore
dotnet build saviaup.print-agent.sln
dotnet test saviaup.print-agent.sln
dotnet run --project saviaup.print-agent.Worker
```

En la primera ejecución el agente abre una conexión de descubrimiento sin privilegios y espera a que un administrador lo seleccione en Savia Up. El backend entrega entonces una credencial de dispositivo por esa conexión y el agente la guarda cifrada con Windows DPAPI. Después sincroniza las impresoras instaladas, abre SignalR autenticado, consulta pendientes periódicamente, envía heartbeat y procesa la cola local. Cuando un administrador pulsa **Buscar impresoras**, recibe una solicitud dirigida por SignalR, vuelve a consultar las colas de Windows y sincroniza el resultado con el backend.

Los datos se almacenan por defecto en `%ProgramData%\SaviaUp\PrintAgent`:

- `print-queue.db`: cola SQLite e idempotencia por `PrintJobId`.
- `device-token.dat`: credencial cifrada para la máquina local.
- `logs\agent-YYYYMMDD.log`: logs sin tokens ni payloads.

Consulta [ARCHITECTURE.md](./ARCHITECTURE.md) para el diseño y [install.MD](./install.MD) para publicación e instalación como servicio.

## Impresión soportada

- `WINDOWS_SPOOLER`: cola instalada en Windows, incluidas impresoras USB administradas por el spooler.
- `NETWORK` y `ESC_POS_NETWORK`: TCP RAW a IP/puerto, normalmente 9100.
- Ticket ESC/POS para papel de 58 mm u 80 mm.
- Marca visible `*** REIMPRESIÓN ***` cuando el backend crea una ejecución de reimpresión.

El spooler solo confirma que aceptó el documento; no puede garantizar que el papel salió físicamente. Los errores reportados por la API del spooler o por TCP sí quedan registrados y reintentados.

## Control de versiones

No crear commits, hacer push ni abrir Pull Requests salvo solicitud explícita del usuario.
