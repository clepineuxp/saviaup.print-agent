# Instalador de Windows

SaviaUpPrintAgent.iss genera un único instalador gráfico EXE para Windows x64. Instala el
agente autocontenido en Program Files, crea el servicio SaviaUpPrintAgent, lo inicia
automáticamente y conserva la cola y la credencial local en ProgramData.

El EXE del instalador, el asistente y la entrada de *Aplicaciones instaladas* usan los assets
oficiales versionados en `installer/assets/`; no los sustituya por iconos generados o no oficiales.

Si se ejecuta nuevamente en un equipo que ya tiene el agente, el asistente permite elegir
**Actualizar** (conserva la cola, credencial y vinculación) o **Desinstalar**. Durante una
actualización detiene el servicio antes de reemplazar binarios. La desinstalación, tanto desde
este asistente como desde *Aplicaciones instaladas* de Windows, detiene y elimina el servicio
`SaviaUpPrintAgent`; los datos en ProgramData se conservan para evitar perder una cola por error.

En un equipo Windows de publicación con el SDK de .NET 10 e Inno Setup 6, ejecuta:

    .\scripts\New-Installer.ps1 -BackendUrl 'https://api.saviaup.com' -Version '1.0.0'

El resultado contiene ambos archivos:

    artifacts\installer\<version>\SaviaUpPrintAgent-Setup-<version>.exe
    artifacts\installer\<version>\SaviaUpPrintAgent-Setup.exe

Sube ambos al almacenamiento de descargas del VPS mediante HTTPS y configura en el backend:

    Printing__AgentDownloadUrl=https://downloads.saviaup.com/SaviaUpPrintAgent-Setup.exe

Nunca empaquetes códigos de vinculación, tokens ni credenciales. Después de instalar, el
usuario solo debe aceptar el aviso de administrador; el agente aparecerá como disponible en
Savia Up y se vincula desde la pantalla de Impresión.
