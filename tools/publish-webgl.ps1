# publish-webgl.ps1
#
# Take a finished WebGL build from docs/build-webgl/<app> and make it servable at the
# repo's GitHub Pages root, then push.
#
# GitHub Pages serves the repo root (branch `main`, folder `/`), and the game needs to
# be reachable at a single tidy URL, so each app gets its own subfolder:
#
#   https://<user>.github.io/<repo>/<app>/index.html
#
#   powershell -File tools/publish-webgl.ps1 -App expedition
#   powershell -File tools/publish-webgl.ps1 -App expedition -Push

param(
    [Parameter(Mandatory)]
    [ValidateSet('training-range', 'expedition')]
    [string]$App,

    [switch]$Push,
    [string]$Remote = 'origin',
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repo "docs\build-webgl\$App"
$index = Join-Path $buildDir 'index.html'

if (-not (Test-Path -LiteralPath $index)) {
    Write-Output "No build found at $buildDir"
    Write-Output ''
    Write-Output 'Build it first:'
    Write-Output "  powershell -File tools/build-webgl.ps1 -App $App"
    exit 1
}

$buildSize = (Get-ChildItem -LiteralPath $buildDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Output ("found build: {0}  {1:N1} MB" -f $buildDir, $buildSize)

# --------------------------------------------------- guard against a broken page
$html = Get-Content -LiteralPath $index -Raw
if ($html -notmatch 'createUnityInstance|unityInstance') {
    Write-Output 'index.html does not look like a Unity WebGL loader; refusing to publish.'
    exit 1
}

# ------------------------------------------------------------- landing page
$landing = @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Duck — magic-orb shooter skeleton</title>
  <style>
    :root { color-scheme: dark; }
    body { margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center;
           background:#0b0d10; color:#e8ecf1; font:16px/1.6 system-ui,-apple-system,"Segoe UI",sans-serif; }
    main { max-width:640px; padding:2rem; }
    h1 { font-size:1.6rem; margin:0 0 .35rem; }
    p.lede { color:#9aa7b4; margin:0 0 1.6rem; }
    a.card { display:block; padding:1rem 1.15rem; margin:.6rem 0; border:1px solid #232a33;
             border-radius:12px; text-decoration:none; color:inherit; background:#12161b;
             transition:border-color .15s, transform .15s; }
    a.card:hover { border-color:#3d6b5a; transform:translateY(-1px); }
    a.card strong { display:block; font-size:1.05rem; }
    a.card span { color:#8b98a5; font-size:.9rem; }
    footer { margin-top:1.8rem; color:#6b7885; font-size:.82rem; }
    footer a { color:#8fb8a8; }
  </style>
</head>
<body>
  <main>
    <h1>Duck</h1>
    <p class="lede">Two playable apps built from one shared combat core.</p>
    <a class="card" href="./training-range/">
      <strong>Training Range</strong>
      <span>Bare court, target dummy. Aim, fire, reload, camera feel.</span>
    </a>
    <a class="card" href="./expedition/">
      <strong>Grove Camp</strong>
      <span>Same core with a merchant, enemies, a HUD and a full art pass.</span>
    </a>
    <footer>
      <a href="https://github.com/gushu8861/duck">Source on GitHub</a> ·
      WASD move · Shift sprint · Mouse aim · Left mouse fire
    </footer>
  </main>
</body>
</html>
"@

$rootIndex = Join-Path $repo 'index.html'
if (-not (Test-Path -LiteralPath $rootIndex)) {
    Set-Content -LiteralPath $rootIndex -Value $landing -Encoding UTF8
    Write-Output 'wrote repo-root index.html (landing page linking both apps)'
} else {
    Write-Output 'repo-root index.html already exists; left untouched'
}

# Pages needs this to skip Jekyll processing of the Unity output.
$nojekyll = Join-Path $repo '.nojekyll'
if (-not (Test-Path -LiteralPath $nojekyll)) {
    New-Item -ItemType File -Path $nojekyll | Out-Null
    Write-Output 'wrote .nojekyll'
}

Write-Output ''
Write-Output 'Next: commit docs/build-webgl + index.html + .nojekyll, then enable Pages.'

if ($Push) {
    $git = (Get-Command git -ErrorAction SilentlyContinue)
    if (-not $git) { Write-Output 'git not found on PATH'; exit 1 }

    Push-Location $repo
    try {
        git add -A 2>&1 | Out-Null
        git commit -q -m "Publish $App WebGL build" 2>&1 | Out-Null
        git push $Remote "HEAD:refs/heads/$Branch" 2>&1 | ForEach-Object { Write-Output $_ }
        Write-Output "pushed to $Remote/$Branch"
    } finally {
        Pop-Location
    }
}

Write-Output ''
Write-Output 'Enable Pages once (branch main, folder /), then the app is live at:'
Write-Output "  https://gushu8861.github.io/duck/$App/"
exit 0
