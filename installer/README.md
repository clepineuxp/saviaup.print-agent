# Instalador de Windows

SaviaUpPrintAgent.iss genera un único instalador gráfico EXE para Windows x64. Instala el
agente autocontenido en Program Files, crea el servicio SaviaUpPrintAgent, lo inicia
automáticamente y conserva la cola y la credencial local en ProgramData.

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
