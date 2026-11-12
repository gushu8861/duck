# compile-check.ps1
#
# Headless compile gate for both apps. Called by a Kiro hook after any C# save,
# and useful on its own before a commit.
#
# Finds the Unity editor from (in order): $env:UNITY_EDITOR, the Unity Hub
# editors-v2.json, then a few standard install paths. Exits non-zero if either
# app fails to compile, printing only the unique CS errors.

param(
    [string]$Unity = $env:UNITY_EDITOR,
    [string[]]$Only = @()
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Find-Unity {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path -LiteralPath $Explicit)) { return $Explicit }

    $hub = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hub) {
        try {
            $json = Get-Content -LiteralPath $hub -Raw | ConvertFrom-Json
            $first = $json.data | Select-Object -First 1
            if ($first -and $first.location) { return $first.location[0] }
        } catch { }
    }

    foreach ($p in @(
        'C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe',
        'D:\app\*\Editor\Unity.exe',
        'C:\Program Files\Unity\Editor\Unity.exe'
    )) {
        $hit = Get-Item -Path $p -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$unityPath = Find-Unity -Explicit $Unity
if (-not $unityPath) {
    Write-Output 'compile-check: Unity editor not found.'
    Write-Output 'Set $env:UNITY_EDITOR to the Unity.exe path, then re-run.'
    exit 2
}

$apps = Get-ChildItem -LiteralPath (Join-Path $repo 'apps') -Directory
if ($Only.Count -gt 0) { $apps = $apps | Where-Object { $Only -contains $_.Name } }

$failed = 0
foreach ($app in $apps) {
    $proj = Join-Path $app.FullName 'unity'
    if (-not (Test-Path -LiteralPath $proj)) { continue }
    $log = Join-Path ([System.IO.Path]::GetTempPath()) "compile-$($app.Name).log"

    Write-Output "compiling $($app.Name)..."
    $proc = Start-Process -FilePath $unityPath -PassThru -Wait -ArgumentList @(
        '-batchmode', '-quit', '-nographics',
        '-projectPath', $proj,
        '-logFile', $log
    )

    $errors = @()
    if (Test-Path -LiteralPath $log) {
        $errors = Select-String -Path $log -Pattern 'error CS' |
                  ForEach-Object { $_.Line } | Sort-Object -Unique
    }

    if ($errors.Count -gt 0) {
        $failed++
        Write-Output "  FAILED ($($errors.Count) unique errors)"
        $errors | Select-Object -First 40 | ForEach-Object { Write-Output "    $_" }
    } else {
        Write-Output "  ok (exit $($proc.ExitCode))"
    }
}

if ($failed -gt 0) { exit 1 }
Write-Output 'compile-check: all apps compile.'
exit 0
