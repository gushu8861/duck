# check-foundation-drift.ps1
#
# Guard the two-app contract.
#
# foundation/Assets/DuckGame/{Runtime,Shaders,Art} is the canonical copy of the
# skeleton. Every app carries its own copy under
# apps/<app>/unity/Assets/DuckGame/{same folders}.
#
# The failure this catches: someone edits the copy inside an app, it works, and
# the next `sync-foundation` silently erases it. Exit code 1 + a loud report so
# a Kiro hook (or CI) can stop the save.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$foundation = Join-Path $repo 'foundation\Assets\DuckGame'
$apps = Get-ChildItem -LiteralPath (Join-Path $repo 'apps') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'unity\Assets\DuckGame') }

$owned = 'Runtime', 'Shaders', 'Art'
$drift = New-Object 'System.Collections.Generic.List[string]'
$checked = 0

foreach ($app in $apps) {
    foreach ($sub in $owned) {
        $canonical = Join-Path $foundation $sub
        $copy = Join-Path $app.FullName "unity\Assets\DuckGame\$sub"
        if (-not (Test-Path -LiteralPath $copy)) { continue }

        $canonFiles = Get-ChildItem -LiteralPath $canonical -Recurse -File |
                      Where-Object { $_.Extension -ne '.meta' }
        foreach ($f in $canonFiles) {
            $rel = $f.FullName.Substring($canonical.Length).TrimStart('\')
            $twin = Join-Path $copy $rel
            $checked++
            if (-not (Test-Path -LiteralPath $twin)) {
                $drift.Add("MISSING in $($app.Name): $sub/$rel")
                continue
            }
            $a = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
            $b = (Get-FileHash -LiteralPath $twin -Algorithm SHA256).Hash
            if ($a -ne $b) {
                $drift.Add("DRIFT in $($app.Name): $sub/$rel")
            }
        }

        # files that exist only inside the app copy
        $appFiles = Get-ChildItem -LiteralPath $copy -Recurse -File |
                    Where-Object { $_.Extension -ne '.meta' }
        foreach ($f in $appFiles) {
            $rel = $f.FullName.Substring($copy.Length).TrimStart('\')
            if (-not (Test-Path -LiteralPath (Join-Path $canonical $rel))) {
                $drift.Add("APP-ONLY in $($app.Name): $sub/$rel  (move to foundation if it is skeleton code)")
            }
        }
    }
}

Write-Output "foundation-drift: checked=$checked drift=$($drift.Count)"
foreach ($d in $drift) { Write-Output "  $d" }

if ($drift.Count -gt 0) {
    Write-Output ''
    Write-Output 'Fix: edit foundation/Assets/DuckGame/... then run tools/sync-foundation.ps1'
    exit 1
}
exit 0
