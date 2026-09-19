# Savia Up Print Agent

Agente local de impresión automática para Savia Up. Se ejecutará en segundo plano dentro del restaurante y permitirá imprimir comandas en impresoras instaladas en Windows o disponibles por red, incluso cuando la aplicación web esté cerrada.

> Estado actual: repositorio inicial. La solución .NET y sus componentes todavía no se han generado.

## Objetivo

El flujo previsto es:

```text
saviaup.backend
  └── persiste PrintJob
      └── notifica por SignalR
          └── Savia Up Print Agent descarga el trabajo
              └── lo guarda en SQLite
                  └── imprime
                      └── reporta el resultado al backend
```

El backend será siempre la fuente de verdad. SignalR acelerará la entrega, pero el agente también recuperará trabajos pendientes después de una desconexión o reinicio.

## Alcance previsto

- Worker Service compatible con Windows Service.
- Conexión segura al backend mediante HTTPS/WSS.
- Notificaciones en tiempo real con SignalR y recuperación de pendientes por API.
- Cola local persistente con SQLite.
- Idempotencia por `PrintJobId` para evitar impresiones duplicadas.
- Reintentos configurables con backoff.
- Heartbeat y reporte de estados al backend.
- Impresión mediante Windows Print Spooler.
- Impresión ESC/POS por red/TCP RAW.
- Tickets configurables para papel de 58 mm y 80 mm.
- Aislamiento estricto por organización, sede y agente.

## Arquitectura prevista

La implementación seguirá la arquitectura hexagonal utilizada en `saviaup.backend`:

```text
saviaup.printangent.Shared
saviaup.printangent.Domain
saviaup.printangent.Core
saviaup.printangent.Infrastructure
saviaup.printangent.Worker
tests/
```

Core contendrá el procesamiento y las reglas de la cola. Infrastructure implementará SQLite, SignalR/HTTP, almacenamiento seguro y drivers de impresión. Worker alojará y compondrá los servicios en segundo plano.

## Requisitos de desarrollo

- Windows 10 u 11 para validar integración con el spooler.
- .NET SDK 10.
- Acceso a una instancia compatible de `saviaup.backend` cuando se implemente la integración.
- Una impresora Windows o ESC/POS de red para pruebas físicas; las pruebas automatizadas usarán drivers simulados.

## Desarrollo

Cuando exista la solución, los comandos base serán:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project saviaup.printangent.Worker
```

Los nombres definitivos de archivos de configuración y opciones se documentarán al crear el host. No se deben guardar tokens de dispositivo, códigos de pairing ni otros secretos en Git.

## Persistencia local

SQLite conservará los trabajos descargados antes de imprimirlos. Como mínimo, cada entrada registrará su identificador remoto, impresora, payload estructurado, estado, intentos, fechas UTC y último error. La restricción única de `PrintJobId` impedirá reprocesar eventos duplicados.

Los archivos SQLite, logs y publicaciones locales están excluidos mediante `.gitignore`.

## Documentación futura

Durante la implementación se agregarán:

- `ARCHITECTURE.md`: componentes, lifecycle del trabajo, pairing, recuperación, retry, idempotencia y seguridad multi-tenant.
- `install.md`: publicación, instalación como Windows Service, configuración, actualización y desinstalación.

## Repositorios relacionados

- `saviaup.backend`: persistencia de trabajos, autenticación del agente, API y hub de impresión.
- `saviaup.frontend`: administración de agentes, impresoras, zonas y cola de impresión.

## Control de versiones

No crear commits, hacer push ni abrir Pull Requests salvo solicitud explícita del usuario.
