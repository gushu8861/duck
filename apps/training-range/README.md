# Training Range — application 1

The minimal slice of the skeleton: one court, one dummy, nothing to hide behind.

This app exists to answer one question before any content is added — **does aiming
and firing feel good?** If the answer is no, no amount of art fixes it.

## Run it

1. Open this folder (`apps/training-range/unity`) in Unity **2022.3.62f3c1**.
2. Open `Assets/DuckGame/Generated/v03/Playground.unity`.
3. Press Play.

## Controls

| Input | Action |
|---|---|
| `WASD` | Move, relative to camera facing |
| `Shift` | Sprint |
| `Space` | Jump |
| `Ctrl` | Slide |
| Mouse | Aim (shoulder camera) |
| Right mouse (hold) | Aim down sights |
| Left mouse | Fire one orb |
| Left mouse (hold) | Continuous fire, gated by the shot interval |
| `1` `2` `3` `4` | Play a hat animation (call, tilt, flap, flinch) |
| `Esc` | Release the mouse; click to recapture |

## What is in here

- Camera-relative locomotion with sprint, jump and slide
- Shoulder camera with ADS blend and obstacle handling
- Orb weapon: rate-gated firing, single magazine with automatic refill
- Target dummy that registers orb impacts
- Toon shaders with a dedicated outline pass

## What is deliberately absent

No enemies, no economy, no HUD beyond the minimum, no menu. Those live in the
other app. Keeping this one bare is the point: it is the readable reference for
the combat core.

## Code layout

```
Assets/DuckGame/
├── Runtime/      foundation code (copied from foundation/, do not edit here)
├── Shaders/      foundation shaders
├── Art/          goose rig
└── Generated/v03/
    ├── Playground.unity   entry scene
    ├── OrbConfig.asset    all weapon tunables
    └── *.mat              the range's material set
```

To change combat behaviour, edit `foundation/` then run
`tools/sync-foundation.ps1`. Edits made to `Assets/DuckGame/Runtime/` in this app
are reported as drift and will be overwritten by the next sync.
