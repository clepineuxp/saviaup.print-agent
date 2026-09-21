# Arquitectura del agente de impresión

## Vista general

```text
Pedido / nuevos ítems
        ↓
saviaup.backend + PostgreSQL
        ↓ commit de Order + PrintJob
SignalR: OnPrintJobAvailable
        ↓
SaviaUp Print Agent
        ↓ descarga autenticada
SQLite local (restricción única PrintJobId)
        ↓
Renderer ESC/POS 58/80 mm
        ↓
Windows Spooler o TCP RAW 9100
        ↓
estado PROCESSING / PRINTED / FAILED al backend
```

PostgreSQL es la fuente de verdad. SignalR solo reduce la latencia; el agente consulta `GET /api/printing/agent/jobs/pending` en cada ciclo y tras reconectarse. Por ello un evento perdido, un reinicio del backend o una caída temporal de Internet no elimina la comanda.

El descubrimiento de impresoras también usa el grupo SignalR autenticado del agente. Ante `OnPrinterDiscoveryRequested`, el worker enumera las colas instaladas en Windows y publica el inventario en `POST /api/printing/agent/printers/sync`. El backend lo conserva como descubrimiento disponible, separado de las impresoras ya configuradas; seleccionar y guardar una cola sigue siendo una acción administrativa explícita.

## Capas

- `Shared`: resultado común para fallos esperados.
- `Domain`: contratos remotos, entidad `LocalPrintJob`, opciones y puertos (`ILocalPrintQueue`, `IBackendClient`, `IPrinterDriver`, `ITicketRenderer`).
- `Core`: `PrintQueueProcessor`, responsable de ingestión idempotente, renderizado, elección de driver, transiciones y sincronización de estados.
- `Infrastructure`: SQLite, cliente HTTPS, cliente SignalR, DPAPI, identidad de dispositivo, descubrimiento Windows, renderer ESC/POS y drivers.
- `Worker`: Windows Service/host, descubrimiento, vinculación, polling, heartbeat, recuperación y composición de DI.

Core no conoce EF, SignalR, Windows ni TCP. Añadir un driver futuro (Star, Zebra o Bluetooth) requiere implementar `IPrinterDriver` y registrarlo en DI.

## Ciclo de vida de un trabajo

1. El backend determina las zonas por rutas explícitas de producto; si no existen, usa las rutas de la categoría.
2. Crea un `PrintJob` por impresora de la zona, con estado `PENDING`, dentro del mismo `ApplicationDbContext` que guarda los nuevos ítems de la orden.
3. Tras `SaveChanges`, el backend notifica exclusivamente al grupo SignalR derivado del `AgentId` autenticado.
4. El agente descarga los pendientes asignados y primero los inserta en SQLite.
5. SQLite rechaza duplicados mediante el índice único de `PrintJobId`. Un trabajo local `PRINTED` nunca vuelve a elegirse automáticamente.
6. El procesador marca `PROCESSING`, renderiza el payload estructurado y usa el driver correspondiente.
7. En éxito marca `PRINTED`; en fallo persiste `FAILED`, error, intento y próxima ejecución con backoff exponencial acotado.
8. Los estados remotos pendientes permanecen en un outbox local hasta que el backend confirma su recepción.

Si el proceso muere durante `PROCESSING`, el arranque lo recupera como `PENDING`. Esta recuperación puede repetir un trabajo cuya impresión física terminó justo antes de una caída y cuya confirmación local no alcanzó a persistirse; es una limitación inevitable sin confirmación transaccional del hardware. El identificador local evita duplicaciones causadas por eventos o polling normales.

## Descubrimiento, vinculación y autenticación

1. Sin credencial, el agente abre `/hubs/printing-discovery` con la URL genérica del backend y anuncia solo identificador estable, hostname, sistema operativo, versión e IP local.
2. La pantalla de Impresión consulta los equipos conectados y un administrador escoge el equipo y la sede. El navegador no escanea IPs: esa operación está bloqueada por los navegadores y el canal saliente del agente funciona también con firewalls normales.
3. El backend crea o actualiza el agente, emite un token aleatorio de larga duración y lo entrega únicamente por la conexión SignalR que anunció el dispositivo.
4. PostgreSQL guarda únicamente SHA-256 del token. El agente guarda el token con DPAPI `LocalMachine` en `%ProgramData%`.
5. Cada llamada REST y la conexión SignalR operativa usan el esquema `PrintAgent`. El backend obtiene `TenantId`, `LocationId` y `AgentId` de la credencial; ningún request del agente elige el tenant.

El hub de descubrimiento no expone trabajos, organizaciones ni impresoras y los registros desaparecen al desconectarse. Deshabilitar o desvincular un agente revoca sus credenciales y cancela trabajos abiertos; al quedar sin token, vuelve a anunciarse para que un administrador lo vincule de nuevo.

## Multi-tenancy y sedes

`Location`, `PrintAgent`, `Printer`, `PrintingZone`, tablas de rutas y `PrintJob` contienen `TenantId`. Repositorios y casos de uso reciben el tenant desde el contexto firmado y filtran cada consulta. Las rutas de agente no aceptan `TenantId`; lo derivan del token. El Hub agrega la conexión únicamente al grupo `printing:agent:{agentId}` autenticado.

El modelo soporta `Location 1 → N Agents`, `Agent 1 → N PrintingZones` y `PrintingZone N ↔ N Printers`. En particular, una sede puede operar con un solo agente y múltiples zonas e impresoras.

## Payload y reimpresión

El backend envía JSON estructurado (`KitchenOrder`) con número, mesa, mesero, fecha UTC, ítems, modificadores y notas. No envía HTML. El renderer genera comandos ESC/POS según ancho 58/80 mm. Una reimpresión crea un nuevo `PrintJob`, conserva `OriginalPrintJobId`, registra usuario/fecha de solicitud y cambia `isReprint` en el payload para imprimir la marca visible.

## Recuperación y observabilidad

- SignalR usa reconexión automática; REST polling continúa siendo el mecanismo de recuperación.
- Los intervalos, reintentos y límites se configuran mediante `PrintAgent` Options.
- Fechas persistidas y logs usan UTC.
- Los logs no contienen tokens, códigos de vinculación ni payloads completos.
- SQLite y los logs viven fuera del directorio de la aplicación para sobrevivir actualizaciones.
