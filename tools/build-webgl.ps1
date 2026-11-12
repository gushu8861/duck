# build-webgl.ps1
#
# Build a playable WebGL target for one app and leave a static folder that any host
# can serve. Called by hand, or later by a publish step.
#
#   powershell -File tools/build-webgl.ps1 -App training-range -Serve
#
# Requires the Unity WebGL Build Support module for the pinned editor version.

param(
    [ValidateSet('training-range', 'expedition')]
    [string]$App = 'training-range',
    [string]$Unity = $env:UNITY_EDITOR,
    [string]$Out,
    [switch]$Serve
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "apps\$App\unity"

if (-not (Test-Path -LiteralPath $proj)) { throw "project not found: $proj" }

# ---------------------------------------------------------------- find editor
function Find-Unity {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) { return $Explicit }

    $hub = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hub) {
        try {
            $json = Get-Content -LiteralPath $hub -Raw | ConvertFrom-Json
            $loc = ($json.data | Select-Object -First 1).location[0]
            if ($loc -and (Test-Path -LiteralPath $loc)) { return $loc }
        } catch { }
    }

    foreach ($pattern in @(
        'C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe',
        'D:\app\*\Editor\Unity.exe',
        'C:\Program Files\Unity\Editor\Unity.exe'
    )) {
        $hit = Get-Item -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$unityPath = Find-Unity -Explicit $Unity
if (-not $unityPath) { throw 'Unity editor not found. Set $env:UNITY_EDITOR.' }

# --------------------------------------------------------- require WebGL module
$editorRoot = Split-Path (Split-Path $unityPath -Parent) -Parent
$webglModule = Join-Path $editorRoot 'Editor\Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path -LiteralPath $webglModule)) {
    throw "WebGL Build Support is not installed for this editor. Expected: $webglModule"
}

# ------------------------------------------------------------------- build
if (-not $Out) { $Out = Join-Path $repo "docs\build-webgl\$App" }
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$log = Join-Path $Out "build-$App.log"

Write-Output "editor : $unityPath"
Write-Output "project: $proj"
Write-Output "output : $Out"
Write-Output 'building (this takes a while for a first WebGL build)...'

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $unityPath -PassThru -Wait -NoNewWindow -ArgumentList @(
    '-quit', '-batchmode', '-nographics',
    '-projectPath', $proj,
    '-buildTarget', 'WebGL',
    '-executeMethod', 'DuckGame.EditorTools.WebGLBuilder.Build',
    '-logFile', $log
)
$sw.Stop()

# Unity batchmode exits 0 even when compilation fails, so never trust the code alone.
$compileErrors = @()
if (Test-Path -LiteralPath $log) {
    $compileErrors = Select-String -Path $log -Pattern 'error CS' |
                     ForEach-Object { $_.Line } | Sort-Object -Unique
}
$builderFailed = $false
if (Test-Path -LiteralPath $log) {
    $builderFailed = [bool](Select-String -Path $log -Pattern '\[WebGLBuilder\] .*fail' -Quiet)
}

$index = Join-Path $Out 'index.html'
$ok = (Test-Path -LiteralPath $index) -and ($compileErrors.Count -eq 0) -and (-not $builderFailed)

if (-not $ok) {
    Write-Output ''
    Write-Output "BUILD FAILED (exit $($proc.ExitCode), $([int]$sw.Elapsed.TotalSeconds)s)"
    if ($compileErrors.Count -gt 0) {
        Write-Output "compile errors ($($compileErrors.Count)):"
        $compileErrors | Select-Object -First 25 | ForEach-Object { Write-Output "  $_" }
    }
    if ($builderFailed) {
        Select-String -Path $log -Pattern '\[WebGLBuilder\]' |
            Select-Object -Last 8 | ForEach-Object { Write-Output "  $($_.Line)" }
    }
    Write-Output "log: $log"
    exit 1
}

$mb = (Get-ChildItem -LiteralPath $Out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Output ''
Write-Output ("BUILD OK  {0:N1} MB  {1:N0}s" -f $mb, $sw.Elapsed.TotalSeconds)
Write-Output "index: $index"

if ($Serve) {
    Write-Output ''
    Write-Output 'serving on http://localhost:8080  (Ctrl+C to stop)'
    Push-Location $Out
    try { npx --yes serve . -l 8080 } finally { Pop-Location }
}
exit 0
