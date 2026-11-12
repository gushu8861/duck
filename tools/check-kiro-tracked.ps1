# check-kiro-tracked.ps1
#
# Kiroween rule: the repository MUST contain /.kiro at the project root, and
# /.kiro (or any subfolder) MUST NOT appear in .gitignore. Getting this wrong can
# disqualify the submission, and it is a one-line mistake to make.
#
# Also verifies the .kiro tree actually carries content (steering, specs, hooks),
# because an empty tracked folder proves nothing to a judge.

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$problems = New-Object 'System.Collections.Generic.List[string]'

$kiro = Join-Path $repo '.kiro'
if (-not (Test-Path -LiteralPath $kiro)) {
    $problems.Add('.kiro/ directory is MISSING at the repo root.')
} else {
    foreach ($sub in 'steering', 'specs', 'hooks') {
        $p = Join-Path $kiro $sub
        if (-not (Test-Path -LiteralPath $p)) {
            $problems.Add(".kiro/$sub/ is missing.")
            continue
        }
        $n = (Get-ChildItem -LiteralPath $p -Recurse -File -ErrorAction SilentlyContinue).Count
        if ($n -eq 0) { $problems.Add(".kiro/$sub/ is empty - add real content, an empty folder scores nothing.") }
    }
}

# .gitignore must not exclude .kiro in any form
foreach ($gi in @('.gitignore', 'apps\.gitignore', 'foundation\.gitignore')) {
    $path = Join-Path $repo $gi
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $line = Select-String -Path $path -Pattern '^\s*/?\.kiro' | Select-Object -First 1
    if ($line) {
        $problems.Add("$gi line $($line.LineNumber) excludes .kiro: '$($line.Line.Trim())'")
    }
}

Write-Output "kiro-tracked: problems=$($problems.Count)"
foreach ($p in $problems) { Write-Output "  $p" }

if ($problems.Count -gt 0) {
    Write-Output ''
    Write-Output 'Remove any .kiro exclusion from .gitignore, then: git add .kiro && git status --short'
    exit 1
}

# Is it actually going to be committed?
if (Get-Command git -ErrorAction SilentlyContinue) {
    Push-Location $repo
    try {
        $ignored = git check-ignore -q .kiro 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Output '.kiro is currently matched by a git ignore rule (git check-ignore). Fix before submitting.'
            exit 1
        }
        $tracked = (git ls-files .kiro 2>$null | Measure-Object).Count
        Write-Output "kiro files tracked by git: $tracked"
        if ($tracked -eq 0) {
            Write-Output 'Not tracked yet - remember: git add .kiro'
        }
    } finally {
        Pop-Location
    }
}

exit 0
