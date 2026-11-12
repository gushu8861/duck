inclusion: always
---

# Project Structure

## Top-level layout

```
/
├── .kiro/                     Kiro steering, specs, hooks, agents  (NEVER gitignored)
├── foundation/                canonical shared skeleton (source, not a package)
│   ├── Assets/DuckGame/       Runtime, Shaders, Art
│   └── README.md
├── apps/
│   ├── training-range/        Application 1 — standalone Unity project
│   │   ├── unity/             Assets, Packages, ProjectSettings
│   │   └── README.md
│   └── expedition/            Application 2 — standalone Unity project
│       ├── unity/
│       └── README.md
├── tools/                     PowerShell helpers (build, sync, verify)
├── docs/                      design notes, screenshots, verification output
├── LICENSE                    MIT — applies to project code
├── CREDITS.md                 third-party asset licences
└── README.md                  entry point for judges
```

## The two-app contract

Both apps are **independent Unity projects**. Each has its own `Assets/`,
`Packages/` and `ProjectSettings/`, and each opens directly in Unity Hub. A judge
can open either one without opening the other.

What they share is **source**, kept in `foundation/` and copied in. DRY in Unity
scenes is a trap: scene files bind scripts by GUID, so a shared package that
lives outside `Assets/` breaks every scene the moment a path changes. Copying a
small, stable skeleton beats a clever abstraction here.

The copies are allowed to diverge in `Assets/DuckGame/Maps/**` (app-owned
content) but **not** in `Assets/DuckGame/Runtime/**`, `Shaders/**` or `Art/**`
(foundation-owned). `tools/sync-foundation.ps1` enforces that boundary and warns
before overwriting an app-local edit to a foundation file.

## Naming conventions

| Kind | Convention | Example |
|---|---|---|
| C# types | PascalCase, one public type per file matching the filename | `OrbShooter.cs` |
| Namespaces | `DuckGame.<Area>` | `DuckGame.Expedition`, `DuckGame.Maps.V12` |
| ScriptableObject assets | PascalCase, no version suffix | `OrbConfig.asset` |
| Scenes | PascalCase, one per intended app entry point | `Playground.unity`, `ExpeditionHub.unity` |
| Materials | Lowercase words with spaces, prefixed by owner when ambiguous | `Player_Feather.mat`, `OrbGlow.mat` |
| Version folders | `V01`..`V12`, numbered by iteration, never renamed | `Maps/V12/` |
| Generated output | `Generated/` under the owning area | `Maps/V12/Generated/` |

Version folders are **append-only history**. A later iteration gets a new `Vnn`
folder; it does not rewrite the previous one. This is why `Maps/` has V07 through
V12 side by side — each holds a scene that still opens.

## Where new content goes

| Adding… | Touch | Never touch |
|---|---|---|
| A new orb type | a new `OrbConfig` asset | `OrbShooter.cs` |
| A new enemy | a prefab + a tuning struct instance | `WaveController.cs` |
| A new level | a new scene under `Maps/Vnn/Generated/` | `Runtime/` |
| A new HUD element | the HUD component of that app | foundation `Runtime/` |
| A new shader | `Shaders/` in foundation, then sync | an app-local shader |

If a content addition forces an edit in `foundation/`, that is a signal the
skeleton needs a new extension point — add the point, then add the content in the
app.

## Inspector wiring rules

- Prefer `[SerializeField]` with sane defaults over `FindObjectOfType` at runtime.
- No `GameObject.Find` in `Update`.
- Cache component lookups in `Awake`, never in `Update`.
- Every serialized reference that the scene needs must be set on the prefab or
  scene object, not resolved by name at runtime.

## File encoding

All source files are UTF-8 without BOM. Scene and prefab files are Unity YAML and
must keep Unity's own format; never hand-edit them with a script that rewrites
line endings.
