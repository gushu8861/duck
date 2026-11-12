# build-webgl.ps1
#
# Build a playable WebGL target for one app. The output is meant to be published
# (GitHub Pages, itch.io) so a judge can play without installing Unity.
#
# Requires the Unity WebGL Build Support module for 2022.3.62f3c1.

param(
    [ValidateSet('training-range', 'expedition')]
    [string]$App = 'training-range',
    [string]$Unity = $env:UNITY_EDITOR,
    [string]$Out
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "apps\$App\unity"

if (-not (Test-Path -LiteralPath $proj)) { throw "project not found: $proj" }
if (-not $Out) { $Out = Join-Path $repo "docs\build-webgl\$App" }

if (-not $Unity) {
    $hub = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hub) {
        $json = Get-Content -LiteralPath $hub -Raw | ConvertFrom-Json
        $Unity = ($json.data | Select-Object -First 1).location[0]
    }
}
if (-not $Unity -or -not (Test-Path -LiteralPath $Unity)) {
    throw "Unity editor not found. Set `$env:UNITY_EDITOR."
}

$module = Join-Path (Split-Path (Split-Path $Unity -Parent) -Parent) 'Editor\Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path -LiteralPath $module)) {
    throw "WebGL Build Support is not installed for this editor: $module"
}

New-Item -ItemType Directory -Path $Out -Force | Out-Null
$log = Join-Path $Out "build-$App.log"

Write-Output "building $App -> $Out"
$proc = Start-Process -FilePath $Unity -PassThru -Wait -ArgumentList @(
    '-quit', '-batchmode', '-nographics',
    '-projectPath', $proj,
    '-buildTarget', 'WebGL',
    '-executeMethod', 'DuckGame.EditorTools.WebGLBuilder.Build',
    '-logFile', $log
)

if ($proc.ExitCode -ne 0) {
    Write-Output "build failed (exit $($proc.ExitCode)); last errors:"
    Select-String -Path $log -Pattern 'error|Error' | Select-Object -Last 20 | ForEach-Object { Write-Output "  $($_.Line)" }
    exit 1
}

Write-Output "build ok: $Out"
Write-Output 'Serve it locally:'
Write-Output "  npx --yes serve `"$Out`""
exit 0
