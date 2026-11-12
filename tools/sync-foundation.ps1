# sync-foundation.ps1
#
# Push the canonical foundation tree into both apps.
#
# foundation/Assets/DuckGame/{Runtime,Shaders,Art}  ->  apps/<app>/unity/Assets/DuckGame/...
#
# Before overwriting, it reports any app copy that has drifted, so a local fix
# that was made in the wrong place is never thrown away silently.

param(
    [switch]$Force,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$foundation = Join-Path $repo 'foundation\Assets\DuckGame'
$apps = Get-ChildItem -LiteralPath (Join-Path $repo 'apps') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'unity') }

if (-not (Test-Path -LiteralPath $foundation)) { throw "foundation not found: $foundation" }

$owned = 'Runtime', 'Shaders', 'Art'

# ---- pre-flight: report drift so the user can rescue an edit ---------------
$drift = New-Object 'System.Collections.Generic.List[string]'
foreach ($app in $apps) {
    foreach ($sub in $owned) {
        $canonical = Join-Path $foundation $sub
        $copy = Join-Path $app.FullName "unity\Assets\DuckGame\$sub"
        if (-not (Test-Path -LiteralPath $canonical)) { continue }
        foreach ($f in (Get-ChildItem -LiteralPath $canonical -Recurse -File | Where-Object { $_.Extension -ne '.meta' })) {
            $rel = $f.FullName.Substring($canonical.Length).TrimStart('\')
            $twin = Join-Path $copy $rel
            if ((Test-Path -LiteralPath $twin) -and
                ((Get-FileHash -LiteralPath $twin).Hash -ne (Get-FileHash -LiteralPath $f.FullName).Hash)) {
                $drift.Add("$($app.Name): $sub/$rel")
            }
        }
    }
}

if ($drift.Count -gt 0) {
    Write-Output "App copies that will be overwritten ($($drift.Count)):"
    $drift | ForEach-Object { Write-Output "  $_" }
    if (-not $Force) {
        Write-Output ''
        Write-Output 'Nothing was written. Move the intended change into foundation/ first,'
        Write-Output 'or re-run with -Force to discard the app copies.'
        exit 1
    }
}

# ---- copy -----------------------------------------------------------------
$written = 0
foreach ($app in $apps) {
    foreach ($sub in $owned) {
        $canonical = Join-Path $foundation $sub
        if (-not (Test-Path -LiteralPath $canonical)) { continue }
        $copy = Join-Path $app.FullName "unity\Assets\DuckGame\$sub"

        foreach ($f in (Get-ChildItem -LiteralPath $canonical -Recurse -File)) {
            $rel = $f.FullName.Substring($canonical.Length).TrimStart('\')
            $dst = Join-Path $copy $rel
            $dstDir = Split-Path -Parent $dst
            if ($WhatIf) {
                Write-Output "would copy $sub/$rel -> $($app.Name)"
                $written++
                continue
            }
            if (-not (Test-Path -LiteralPath $dstDir)) {
                New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
            }
            Copy-Item -LiteralPath $f.FullName -Destination $dst -Force
            $written++
        }
    }
}

Write-Output "sync-foundation: $written file(s) -> $($apps.Count) app(s)"
exit 0
