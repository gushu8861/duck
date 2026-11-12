# check-guid-integrity.ps1
#
# Unity binds scenes to assets by GUID. A duplicate GUID silently makes one of the
# two assets win, and an orphaned .meta (no matching asset) leaves dead entries in
# the project. Both are painful to debug after the fact, cheap to detect now.
#
# Checks every app's Assets tree:
#   - duplicate GUIDs across .meta files
#   - .meta files whose asset no longer exists
#   - assets with no .meta at all

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$apps = Get-ChildItem -LiteralPath (Join-Path $repo 'apps') -Directory

$totalProblems = 0

foreach ($app in $apps) {
    $assets = Join-Path $app.FullName 'unity\Assets'
    if (-not (Test-Path -LiteralPath $assets)) { continue }

    $byGuid = @{}
    $dupes = New-Object 'System.Collections.Generic.List[string]'
    $orphanMeta = New-Object 'System.Collections.Generic.List[string]'

    $metas = Get-ChildItem -LiteralPath $assets -Recurse -File -Filter *.meta -ErrorAction SilentlyContinue
    foreach ($m in $metas) {
        $guid = $null
        try {
            $head = Get-Content -LiteralPath $m.FullName -TotalCount 3 -ErrorAction Stop
            foreach ($l in $head) {
                if ($l -match '^guid:\s*([0-9a-f]{32})') { $guid = $matches[1]; break }
            }
        } catch { }

        if ($guid) {
            if ($byGuid.ContainsKey($guid)) {
                $dupes.Add("$guid  used by  $($byGuid[$guid])  and  $($m.FullName)")
            } else {
                $byGuid[$guid] = $m.FullName
            }
        }

        # a .meta must sit beside a real asset or folder of the same base name
        $target = $m.FullName.Substring(0, $m.FullName.Length - 5)
        if (-not (Test-Path -LiteralPath $target)) {
            $orphanMeta.Add($m.FullName)
        }
    }

    $missingMeta = New-Object 'System.Collections.Generic.List[string]'
    foreach ($item in (Get-ChildItem -LiteralPath $assets -Recurse -ErrorAction SilentlyContinue)) {
        if ($item.Name.EndsWith('.meta')) { continue }
        if (-not (Test-Path -LiteralPath "$($item.FullName).meta")) {
            $missingMeta.Add($item.FullName)
        }
    }

    $count = $dupes.Count + $orphanMeta.Count + $missingMeta.Count
    $totalProblems += $count
    Write-Output "$($app.Name): guid=$($byGuid.Count) dupes=$($dupes.Count) orphanMeta=$($orphanMeta.Count) missingMeta=$($missingMeta.Count)"
    $dupes      | Select-Object -First 10 | ForEach-Object { Write-Output "  DUPE   $_" }
    $orphanMeta | Select-Object -First 10 | ForEach-Object { Write-Output "  ORPHAN ${_}" }
    $missingMeta| Select-Object -First 10 | ForEach-Object { Write-Output "  NOMETA $_" }
}

if ($totalProblems -gt 0) {
    Write-Output ''
    Write-Output "guid-integrity: $totalProblems problem(s). Duplicate GUIDs break scene references - fix before committing."
    exit 1
}

Write-Output 'guid-integrity: clean.'
exit 0
