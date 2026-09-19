# AGENTS.md — Savia Up Print Agent

## Propósito

Este repositorio contiene `saviaup.printangent`, el agente local de impresión automática de Savia Up. El agente se instalará principalmente como Worker Service/Windows Service dentro del restaurante, recibirá trabajos persistidos por `saviaup.backend`, conservará una cola local y enviará las comandas a impresoras Windows o de red sin depender de que el navegador permanezca abierto.

Lee este archivo completo antes de modificar el repositorio. Sus reglas son invariantes del proyecto.

## Control de versiones

- No crear commits, hacer push ni abrir Pull Requests salvo solicitud explícita del usuario en el mensaje actual.
- No sobrescribir ni descartar cambios existentes del usuario.
- Las ramas de funcionalidad usan el prefijo `feature/`.

## Stack y arquitectura

- .NET 10 y C# con nullable reference types.
- Worker Service preparado para ejecutarse como Windows Service.
- Arquitectura hexagonal siguiendo las convenciones de `saviaup.backend` cuando sean aplicables.
- Contenedor de dependencias integrado de Microsoft.
- SignalR Client para notificaciones en tiempo real; el backend sigue siendo la fuente de verdad.
- SQLite para la cola local persistente.
- xUnit para pruebas; Moq únicamente en proyectos de tests.
- Logging estructurado sin incluir credenciales, tokens ni payloads sensibles.

Estructura prevista de la solución:

```text
saviaup.printangent.Shared
        ↑
saviaup.printangent.Domain
        ↑
saviaup.printangent.Core

saviaup.printangent.Infrastructure ──► Domain + Shared
saviaup.printangent.Worker ─────────► Domain + Core + Infrastructure + Shared
tests/saviaup.printangent.Core.Tests
tests/saviaup.printangent.IntegrationTests
```

Reglas de dependencia:

- `Shared` contiene únicamente elementos realmente compartidos.
- `Domain` define entidades, DTO internos, resultados y puertos; no conoce SQLite, SignalR, Windows Spooler ni red.
- `Core` implementa casos de uso, reintentos, idempotencia y reglas de procesamiento; no conoce detalles de infraestructura.
- `Infrastructure` implementa persistencia SQLite, cliente HTTP/SignalR, almacenamiento seguro y drivers de impresión.
- `Worker` compone el host, configuración, servicios en segundo plano y ciclo de vida de Windows Service.
- No introducir dependencias de Core hacia Infrastructure ni de Domain hacia Core.
- Mantener `Program.cs` pequeño y dedicado a composición.

No introducir MediatR, AutoMapper, Autofac, Generic Repository, CQRS, Vertical Slice, Redis, RabbitMQ o una reorganización arquitectónica distinta sin una necesidad aprobada.

## Integración con Savia Up

- Antes de cambiar contratos compartidos, revisar `saviaup.backend` y `saviaup.frontend`.
- Reutilizar sus convenciones de nombres, `Result`, errores, configuración, UTC, DI y contratos siempre que tengan sentido.
- La comunicación remota usa HTTPS/WSS.
- SignalR solo notifica disponibilidad; cada trabajo se recupera desde endpoints autenticados y persistidos por el backend.
- Tras una reconexión, el agente consulta los trabajos pendientes asignados.
- No asumir que una notificación implica que la transacción del backend sigue abierta: el trabajo debe estar confirmado antes de anunciarse.

## Seguridad y multi-tenancy

- El agente se vincula en backend a una organización, sede y `PrintAgent` mediante credenciales propias.
- Nunca aceptar ni enviar un `TenantId` como mecanismo para decidir el alcance autorizado.
- El backend deriva organización, sede y agente desde la credencial autenticada.
- Un agente no puede consultar, recibir, imprimir o actualizar trabajos de otra organización o sede.
- Nunca registrar tokens de dispositivo, códigos de pairing, secretos, credenciales, encabezados de autorización ni payloads completos.
- No guardar secretos en texto plano cuando Windows Data Protection API u otro almacén seguro esté disponible.
- Los archivos de configuración versionados contienen únicamente ejemplos o valores no sensibles.

## Cola local e idempotencia

- SQLite conserva la cola ante reinicios de Windows, caídas de Internet, reinicios del agente y fallos temporales de impresora.
- Estados mínimos: `Pending`, `Processing`, `Printed` y `Failed`, ajustables si el dominio requiere más detalle.
- `PrintJobId` debe tener una restricción única local.
- Un trabajo marcado como `Printed` nunca se imprime otra vez automáticamente por reconexión, polling o eventos duplicados.
- Una reimpresión explícita llega como un nuevo trabajo, conserva `OriginalPrintJobId` y se procesa como una ejecución independiente.
- Recuperar de forma segura trabajos que quedaron `Processing` tras un cierre inesperado.
- Persistir el trabajo local antes de intentar imprimirlo.

## Fiabilidad

- Todas las operaciones async reciben y propagan `CancellationToken`.
- No usar `.Result`, `.Wait()` ni `.GetAwaiter().GetResult()`.
- Usar reintentos con backoff y límites configurables; no crear ciclos agresivos.
- El servicio debe tolerar backend, red o impresora no disponibles y recuperarse sin perder trabajos.
- Reportar heartbeat y transiciones de estado sin convertir cada latido en una escritura innecesaria.
- Las transiciones locales y la confirmación remota deben poder repetirse sin duplicar impresión.
- Los instantes se almacenan en UTC; no persistir fechas locales ambiguas.
- Respetar el apagado ordenado del host y no abandonar operaciones en un estado inconsistente.

## Impresión

- Core depende de un puerto como `IPrinterDriver`; no depende directamente de ESC/POS o APIs de Windows.
- Implementaciones iniciales previstas: Windows Print Spooler y ESC/POS por TCP RAW, normalmente en puerto 9100 configurable.
- Mantener extensibilidad para otros fabricantes o protocolos sin contaminar los casos de uso.
- Los documentos remotos son payloads estructurados, no HTML como formato principal.
- El renderer genera tickets para 58 mm y 80 mm mediante configuración.
- Las reimpresiones deben incluir una marca visible como `*** REIMPRESIÓN ***`.
- No afirmar que una impresión fue exitosa hasta que el driver termine sin error; documentar las limitaciones de confirmación física de cada driver.

## Configuración y datos locales

- Usar Options con validación al iniciar para URL del backend, intervalos, rutas, timeouts y política de reintentos.
- Separar configuración versionable, secretos y estado de ejecución.
- En instalación como servicio, almacenar datos mutables en una ubicación apropiada de `ProgramData`, no junto al ejecutable si requiere permisos elevados.
- Los paths deben ser configurables y funcionar al ejecutar como consola durante desarrollo.
- Nunca versionar bases SQLite, logs, publicaciones ni secretos.

## Código y pruebas

- Mapping explícito; no usar AutoMapper.
- Usar interfaces específicas por capacidad, no un Generic Repository.
- Evitar excepciones como flujo normal; representar fallos esperados con `Result` o un patrón equivalente coherente con el backend.
- Agregar pruebas para idempotencia, recuperación de pendientes, reintentos, reconexión, transición de estados, cancelación y aislamiento de credenciales.
- Las pruebas de infraestructura no deben requerir impresoras físicas; encapsular el driver y usar dobles controlados.
- Para cambios relevantes ejecutar, como mínimo, `dotnet build` y `dotnet test` sobre la solución cuando esta exista.

## Documentación requerida al evolucionar el proyecto

- Mantener `README.md` actualizado con requisitos, ejecución y configuración no sensible.
- Crear y mantener `ARCHITECTURE.md` al implementar los componentes, describiendo lifecycle, pairing, recuperación, retry, idempotencia, zonas e impresoras.
- Crear y mantener `install.md` con publicación, instalación como Windows Service, configuración, actualización y desinstalación.
- Documentar decisiones arquitectónicas relevantes y limitaciones operativas.

