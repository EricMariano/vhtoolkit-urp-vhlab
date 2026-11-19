[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$OutputDirectory,
    [switch]$Development,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'VHUnityURP'
$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
$version = (Select-String -Path $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
if (-not $UnityPath) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $version nao encontrado. Instale pelo Unity Hub ou informe -UnityPath."
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/windows' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$editor = Join-Path (Split-Path $UnityPath) 'Data'
$module = Join-Path $editor 'PlaybackEngines/windowsstandalonesupport'
if (-not (Test-Path -LiteralPath $module)) {
    throw 'Instale Windows Build Support no Unity Hub para este Editor.'
}
Write-Host "Projeto: $project"
Write-Host "Unity exigido: $version"
Write-Host "Saida: $OutputDirectory"
if ($PreflightOnly) { return }
if (Test-Path (Join-Path $project 'Temp/UnityLockfile')) {
    throw 'Feche este projeto no Unity Editor antes de executar o build em batch mode.'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$log = Join-Path $OutputDirectory 'build.log'
$arguments = @(
    '-batchmode', '-quit', '-projectPath', $project,
    '-buildTarget', 'StandaloneWindows64',
    '-executeMethod', 'VHLab.Editor.LabBuild.BuildWindows',
    '-labBuildOutput', (Join-Path $OutputDirectory 'VHLab.exe'),
    '-logFile', $log
)
if ($Development) { $arguments += '-labDevelopment' }
& $UnityPath @arguments
if ($LASTEXITCODE -ne 0) { throw "Build falhou (codigo $LASTEXITCODE). Consulte $log" }
if (-not (Test-Path (Join-Path $OutputDirectory 'VHLab.exe'))) {
    throw "Unity encerrou sem produzir o executavel. Consulte $log"
}
Write-Host "Build concluido em $OutputDirectory. Distribua toda a pasta, incluindo VHLab_Data."
