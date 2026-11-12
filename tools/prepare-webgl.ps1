# prepare-webgl.ps1
#
# Pre-flight for a WebGL publish. Reads files only - it starts no Unity process and
# compiles nothing, so it is safe to run while another Unity job owns the machine.
#
# Checks that everything the build will need is actually in place, so the expensive
# build is not started only to discover a missing scene or a missing module.
#
#   powershell -File tools/prepare-webgl.ps1

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$problems = New-Object 'System.Collections.Generic.List[string]'
$notes    = New-Object 'System.Collections.Generic.List[string]'

# ---------------------------------------------------------------- editor + module
function Find-Unity {
    $hub = Join-Path $env:APPDATA 'UnityHub\editors-v2.json'
    if (Test-Path -LiteralPath $hub) {
        try {
            $json = Get-Content -LiteralPath $hub -Raw | ConvertFrom-Json
            $loc = ($json.data | Select-Object -First 1).location[0]
            if ($loc -and (Test-Path -LiteralPath $loc)) { return $loc }
        } catch { }
    }
    foreach ($pattern in @('D:\app\*\Editor\Unity.exe', 'C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe')) {
        $hit = Get-Item -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$unity = Find-Unity
if ($unity) {
    $notes.Add("editor: $unity")
    $editorRoot = Split-Path (Split-Path $unity -Parent) -Parent
    $webgl = Join-Path $editorRoot 'Editor\Data\PlaybackEngines\WebGLSupport'
    if (Test-Path -LiteralPath $webgl) { $notes.Add('WebGL module: installed') }
    else { $problems.Add("WebGL Build Support missing at $webgl") }
} else {
    $problems.Add('Unity editor not found; set $env:UNITY_EDITOR')
}

# ------------------------------------------------------------------------ apps
foreach ($app in @('training-range', 'expedition')) {
    $proj = Join-Path $repo "apps\$app\unity"
    if (-not (Test-Path -LiteralPath $proj)) { $problems.Add("$app : project folder missing"); continue }

    # builder class present
    $builder = Join-Path $proj 'Assets\DuckGame\Editor\WebGLBuilder.cs'
    if (Test-Path -LiteralPath $builder) {
        if (Select-String -Path $builder -Pattern 'class WebGLBuilder' -Quiet) {
            $notes.Add("$app : WebGLBuilder.cs present")
        } else {
            $problems.Add("$app : WebGLBuilder.cs has no WebGLBuilder class")
        }
    } else {
        $problems.Add("$app : Assets/DuckGame/Editor/WebGLBuilder.cs missing")
    }

    # entry scene listed in build settings AND on disk
    $settings = Join-Path $proj 'ProjectSettings\EditorBuildSettings.asset'
    if (-not (Test-Path -LiteralPath $settings)) { $problems.Add("$app : EditorBuildSettings.asset missing"); continue }

    $paths = Select-String -Path $settings -Pattern '^\s*path:\s*(.+)$' |
             ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }
    $guids = Select-String -Path $settings -Pattern '^\s*guid:\s*([0-9a-f]{32})$' |
             ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }

    if ($paths.Count -eq 0) { $problems.Add("$app : no scene in EditorBuildSettings") }

    $entryFound = $false
    for ($i = 0; $i -lt $paths.Count; $i++) {
        $p = $paths[$i]
        $full = Join-Path $proj ($p -replace '/', '\')
        if (-not (Test-Path -LiteralPath $full)) {
            $problems.Add("$app : build settings list a scene that does not exist: $p")
            continue
        }
        # GUID in build settings must match the scene's .meta, or the build loads a broken scene
        if ($i -lt $guids.Count) {
            $metaGuid = $null
            if (Test-Path -LiteralPath "$full.meta") {
                $m = Select-String -Path "$full.meta" -Pattern '^guid:\s*([0-9a-f]{32})'
                if ($m) { $metaGuid = $m.Matches[0].Groups[1].Value }
            }
            if ($metaGuid -and $metaGuid -ne $guids[$i]) {
                $problems.Add("$app : GUID mismatch for $p (settings $($guids[$i]) vs meta $metaGuid)")
            }
        }
        if ($p -match 'DuckGame') { $entryFound = $true; $notes.Add("$app : entry scene = $p") }
    }
    if (-not $entryFound) { $problems.Add("$app : no DuckGame entry scene in EditorBuildSettings") }

    # URP settings must exist or the toon shaders render magenta
    if (-not (Test-Path -LiteralPath (Join-Path $proj 'Assets\Settings'))) {
        $problems.Add("$app : Assets/Settings (URP assets) missing")
    }
}

# --------------------------------------------------------------------- licence
if (-not (Test-Path -LiteralPath (Join-Path $repo 'LICENSE'))) { $problems.Add('LICENSE missing at repo root') }
$kiroFiles = (Get-ChildItem -LiteralPath (Join-Path $repo '.kiro') -Recurse -File -ErrorAction SilentlyContinue).Count
if ($kiroFiles -eq 0) { $problems.Add('.kiro is missing or empty') } else { $notes.Add(".kiro: $kiroFiles files") }

# ---------------------------------------------------------------------- report
Write-Output '--- ready ---'
$notes | ForEach-Object { Write-Output "  ok   $_" }

Write-Output "--- problems: $($problems.Count) ---"
$problems | ForEach-Object { Write-Output "  FAIL $_" }

if ($problems.Count -gt 0) {
    Write-Output ''
    Write-Output 'Fix these before running tools/build-webgl.ps1 so the build does not fail late.'
    exit 1
}

Write-Output ''
Write-Output 'Ready. When the machine is free, build with:'
Write-Output '  powershell -File tools/build-webgl.ps1 -App expedition'
Write-Output '  powershell -File tools/build-webgl.ps1 -App training-range -Serve'
Write-Output ''
Write-Output 'Output lands in docs/build-webgl/<app>/ and is a plain static folder.'
exit 0
