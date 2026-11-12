# Grove Camp — application 2

The same combat core as the Training Range, carrying progression, characters and
a full art pass. This is the proof that the skeleton is flexible rather than merely
small.

## Run it

1. Open this folder (`apps/expedition/unity`) in Unity **2022.3.62f3c1**.
2. Open `Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity`.
3. Press Play.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move, relative to camera facing |
| `Shift` | Sprint |
| `Space` | Jump |
| `Ctrl` | Slide |
| Mouse | Aim |
| Right mouse (hold) | Aim down sights |
| Left mouse | Fire orbs |
| `F` | Interact (merchant, departure point) |
| `Esc` | Close the open panel / release the mouse |
| `Tab` | Toggle the HUD |

## What is in here

- Everything the Training Range has, plus:
- **Layered toon character** — ink outline pass, sculpted hat with a tailored band
- **Snail merchant** — shop UI, coin economy fed by kills, three upgrade types
  (fire rate, projectile speed, extra projectile)
- **Forest enemies** — charger and fleer roles with distinct silhouettes,
  wind-up tells and hit feedback
- **Atmosphere** — canopy light, ground haze, per-biome palette, orb trail shaders
- **Departure point** — the camp facility where a run is started

## Scene lineage

`Assets/DuckGame/Maps/` keeps every iteration rather than overwriting:

| Folder | Scene | What it added |
|---|---|---|
| `V07` | `WizardGrove` | first grove geometry, movement on real ground |
| `V08` | `MerchantGrove` | merchant character and shop |
| `V09` | `PolishedGrove` | first real art pass, authored SFX |
| `V10` | `EnemyGrove` | enemies with roles and impact feedback |
| `V11` | `StyleGrove` | layered toon look, camera style |
| `V12` | `ExpeditionHub` | **current entry scene** — camp facilities, vending, departure |

Each earlier scene still opens. That is intentional: a version folder is history,
not a draft.

## Code layout

```
Assets/DuckGame/
├── Runtime/       foundation code (copied — do not edit here)
├── Shaders/       foundation shaders
├── Art/           goose rig
├── Maps/V07..V12/ per-iteration content: runtime, generated scenes, materials
├── Expedition/    run, routing, rewards, HUD
├── TSAssets/      imported prototype art and audio set (see the licence note)
└── V04/           movement revision kept for reference
```

## Licence note on `TSAssets/`

`Assets/DuckGame/TSAssets/` holds an imported art and audio set that the V12 scene
references. It is **local prototype material carried in from an outside source for
internal iteration**, not first-party art, and it is not relicensed by this
repository's MIT licence.

If you are reusing this skeleton for your own project: delete `TSAssets/`, point
`ExpeditionHub.unity` at your own art, and you have a clean base. The skeleton code
does not depend on it — only that one scene does.

See `CREDITS.md` at the repo root for the full asset ledger.
