# Foundation — the shared skeleton

Canonical source for everything both apps share. **Edit here, never in an app's
copy.**

```
Assets/DuckGame/
├── Runtime/    combat core (C#)
├── Shaders/    toon, outline, orb glow
└── Art/        goose wizard rig (FBX + SOURCE.txt)
```

## What lives here

| File | Responsibility |
|---|---|
| `GooseMotor.cs` | camera-relative ground/air movement, sprint, slide |
| `ShoulderCamera.cs` | spring-arm shoulder rig, ADS blend, obstacle handling |
| `OrbConfig.cs` | ScriptableObject: rate, burst, spread, speed, lifetime, effects |
| `OrbShooter.cs` | fire gate, rate limit, burst dispatch, muzzle resolution |
| `OrbMagazine.cs` | load/reserve state machine, refill delay |
| `OrbProjectile.cs` | sweep-cast travel, impact dispatch |
| `OrbShotRouter.cs` | `IOrbShotRouter` — routes a shot to pool or instantiate |
| `OrbImpactPool.cs` | pooled impact VFX with expiry |
| `ExpireVisual.cs` | timed self-destroy for pooled effects |
| `HatBulge.cs` | bone-driven local hat deformation |
| `TargetDummy.cs` | hit receiver for range testing |
| `IEnemyShotReceiver.cs` | enemy-side damage intake contract |
| `RuntimeVerification.cs` | in-editor self-checks |
| `GooseToon.shader` | URP 14 toon lighting |
| `GooseOutline.shader` | dedicated outline pass |

## The boundary rule

Foundation code must not reference a content type. No `using DuckGame.Expedition`,
no `using DuckGame.Maps.*`, no direct mention of a scene, prefab or material.

Content reaches the core through three extension points only:

1. **`OrbConfig`** — a ScriptableObject. New orb behaviour is a new asset.
2. **`OrbEffect`** — an abstract class. New impact behaviour is one small subclass.
3. **`IOrbHitReceiver` / `IEnemyShotReceiver` / `IOrbShotRouter`** — interfaces.
   New receivers and delivery strategies implement them.

If a change needs more than these, add an extension point here, then put the
content in the app.

## Syncing

```powershell
# push this tree into both apps (refuses to overwrite app-local edits)
powershell -File tools/sync-foundation.ps1

# prove neither app has drifted
powershell -File tools/check-foundation-drift.ps1
```

Both scripts hash-compare before writing, so an edit made in the wrong place is
reported rather than silently lost.
