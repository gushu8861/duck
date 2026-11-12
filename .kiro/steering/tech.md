inclusion: always
---

# Technology Stack

## Engine

| Thing | Version | Notes |
|---|---|---|
| Unity | **2022.3.62f3c1** | Pinned in `ProjectSettings/ProjectVersion.txt` in both apps. |
| Render pipeline | **URP 14.0.12** | Linear color space. Both apps ship their own URP assets under `Assets/Settings`. |
| Scripting | C# 9, .NET Standard 2.1 | No assembly definitions yet; everything compiles into `Assembly-CSharp`. |
| Text | TextMeshPro 3.0.7 | Present in the manifest, used sparingly. |
| Input | Classic `Input` class | Not the new Input System package. Reads are centralised so this is a one-file change later. |

## Packages

`Packages/manifest.json` is identical in both apps and lists URP, TextMeshPro,
Timeline, Visual Scripting, the IDE integrations and the standard modules. The
lock file is committed so a clone resolves the exact same graph.

No package is imported from a git URL, a private registry, or an Asset Store
purchase. A fresh clone resolves from the public Unity registry alone.

## The foundation is copied, not referenced

`foundation/` is **not** a UPM package that the apps resolve over `file:`. It is a
source snapshot with a manifest and a script that copies it into an app:

```
foundation/Assets/DuckGame/...        # canonical source
apps/*/unity/Assets/DuckGame/...      # the copy each app builds from
```

Why this shape rather than a real package:

- Unity's package system fights with `Assets/` scenes that reference package
  scripts by GUID. A copied snapshot keeps every scene resolvable with no
  package-resolution step, which is what a judge cloning the repo needs.
- A jam team can fork one app and edit the foundation in place without learning
  a package workflow.

The cost is drift between the two copies. `tools/sync-foundation.ps1` closes that
gap by re-copying the canonical tree and reporting any app-local edits that would
be overwritten.

## Assets and licenses

Everything imported carries its licence next to it:

| Asset | Author | Licence | Where |
|---|---|---|---|
| Goose Wizard rig | authored for this project | project MIT | `Apps/DuckGame/Art/` |
| Vending machine | Valentin Laffitte | CC0 | `ThirdParty/ValentinLaffitteVending/` |
| Stone arch | KayKit (Kay Lousberg) | CC0 | `ThirdParty/KayKitHalloweenArch/` |
| Feedback SFX | original synthesis + Kenney samples | CC0 for the Kenney parts | `Maps/V09/Audio/` |

**Hard rule:** no asset may enter this repo unless its licence permits
redistribution in a public source repository *and* a licence file lands beside
it. Anything that cannot meet both conditions stays out, even if the build wants
it.

## Platform targets

- **Editor** — the primary target for judging.
- **WebGL** — used to publish a playable link. Built with the same project; no
  WebGL-specific code paths exist.
- Windows standalone — works, not part of the submission.

## Build commands

```powershell
# open an app
& "<UnityEditor>\Unity.exe" -projectPath apps\training-range\unity
& "<UnityEditor>\Unity.exe" -projectPath apps\expedition\unity

# headless WebGL build (see tools/build-webgl.ps1)
& "<UnityEditor>\Unity.exe" -quit -batchmode -nographics `
    -projectPath apps\training-range\unity `
    -executeMethod DuckGame.EditorTools.WebGLBuilder.Build `
    -logFile build-range.log
```
