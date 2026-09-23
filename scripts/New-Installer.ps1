[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BackendUrl,

    [string]$Version = '',

    [string]$OutputDirectory = '',

    [string]$InnoSetupCompiler = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'saviaup.print-agent.Worker\saviaup.print-agent.Worker.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\SaviaUpPrintAgent.iss'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $projectContent = Get-Content -Raw -LiteralPath $projectPath
    $versionMatch = [regex]::Match($projectContent, '<Version>([^<]+)</Version>')
    if (-not $versionMatch.Success) { throw 'No se encontró la versión del agente en el proyecto.' }
    $Version = $versionMatch.Groups[1].Value
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\installer\$Version"
}

$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\win-x64\$Version"
New-Item -ItemType Directory -Force -Path $publishDirectory, $OutputDirectory | Out-Null

dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'La publicación autocontenida del agente falló.' }

$configurationPath = Join-Path $publishDirectory 'appsettings.json'
$configuration = Get-Content -Raw -LiteralPath $configurationPath | ConvertFrom-Json
$configuration.PrintAgent.BackendUrl = $BackendUrl.TrimEnd('/')
$configuration | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configurationPath -Encoding utf8

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $innoCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    $InnoSetupCompiler = $innoCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or -not (Test-Path -LiteralPath $InnoSetupCompiler)) {
    throw 'No se encontró Inno Setup 6. Instálalo en el equipo de publicación o indica -InnoSetupCompiler.'
}

& $InnoSetupCompiler "/DAppVersion=$Version" "/DSourceDir=$publishDirectory" "/O$OutputDirectory" $installerScript
if ($LASTEXITCODE -ne 0) { throw 'La compilación del instalador falló.' }

$installerPath = Join-Path $OutputDirectory "SaviaUpPrintAgent-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath)) { throw 'Inno Setup no produjo el instalador esperado.' }

$latestInstallerPath = Join-Path $OutputDirectory 'SaviaUpPrintAgent-Setup.exe'
Copy-Item -LiteralPath $installerPath -Destination $latestInstallerPath -Force

Write-Host "Instalador versionado listo: $installerPath"
Write-Host "Instalador estable listo: $latestInstallerPath"
