# Design — Skeleton Foundation

Implements `requirements.md`. Two decisions carry everything else: the foundation
is **copied** rather than packaged, and content reaches the core through
**ScriptableObject config** rather than inheritance.

---

## 1. Repository shape

```
foundation/Assets/DuckGame/       canonical skeleton (Runtime, Shaders, Art)
apps/training-range/unity/        app 1: bare court, one dummy, no content
apps/expedition/unity/            app 2: grove, merchant, enemies, HUD
tools/                            sync, drift, compile, guid, kiro checks
.kiro/                            steering, specs, hooks, agents
```

Each app is a complete Unity project: `Assets/`, `Packages/`, `ProjectSettings/`.
Opening one does not require the other.

## 2. Why copied, not a UPM package

```
option A: local UPM package (`file:../../foundation`)
  + one copy, no drift
  - scenes bind scripts by GUID; moving or version-bumping the package
    invalidates serialized references in every scene that used it
  - judges must resolve a package before anything opens
  - a jam team must learn a package workflow to edit the skeleton

option B: copied snapshot  <-- chosen
  + clone, open, Play. No resolution step
  + an app can edit the skeleton in place without a packaging ceremony
  - copies can drift  ->  tools/sync-foundation.ps1 + check-foundation-drift.ps1
```

The drift risk is real but bounded, and it is cheaper to instrument than to
explain a broken scene to someone trying to run the project for the first time.

## 3. Module boundaries

```
                    ┌────────────────────────────┐
                    │      App content           │
                    │  scenes, prefabs, configs  │
                    └────────────┬───────────────┘
                                 │ references (GUID)
                    ┌────────────▼───────────────┐
                    │      Foundation            │
                    │  Runtime / Shaders / Art   │
                    └────────────────────────────┘
```

The arrow is one-way. Foundation types never reference a content type, which is
what makes a second, unrelated app possible without editing the core.

### Runtime modules

| Module | Responsibility | Depends on |
|---|---|---|
| `GooseMotor` | camera-relative ground/air movement, sprint, slide | `CharacterController` |
| `ShoulderCamera` | spring-arm shoulder rig, ADS blend, obstacle handling | `Camera` |
| `OrbConfig` (SO) | all tunables: rate, burst, spread, speed, lifetime, effects | — |
| `OrbShooter` | fire gate, rate limit, burst dispatch, muzzle resolution | `OrbConfig`, `OrbMagazine` |
| `OrbMagazine` | load / reserve state machine, refill delay | `OrbConfig` |
| `OrbProjectile` | sweep-cast travel and impact dispatch | `OrbConfig` |
| `OrbShotRouter` | `IOrbShotRouter`, routes a shot to a pool or an instantiate path | — |
| `OrbImpactPool` | pooled impact VFX with expiry | `ExpireVisual` |
| `HatBulge` | bone-driven local hat deformation | skinned mesh |
| `TargetDummy` | hit receiver for range testing | `IOrbHitReceiver` |

## 4. The firing pipeline

```
Input ──► OrbShooter.Update
              │  gate: simulation-time >= nextAllowedShot
              │  gate: magazine.TryConsume()
              ▼
          OrbShotContext  (origin, direction, config, burst index)
              │
              ├─► IOrbShotRouter  ──► pool (expedition) or instantiate (range)
              │
              ▼
          OrbProjectile.FixedUpdate
              │  sweep from lastPos to nextPos
              ▼
          IOrbHitReceiver.ReceiveOrbHit(OrbImpactContext)
              │
              ├─► TargetDummy (range)
              └─► CombatBridge ──► enemy health, coins, audio (expedition)
```

Two invariants, both directly from `requirements.md` §2.3–2.4:

1. **Rate is measured in simulation time, not frames.** A hitch must not let a
   burst fire twice in one frame, and must not let a player bank shots.
2. **One shot per committed frame, maximum.** Registering input does not commit a
   shot; the gate does.

## 5. Extension points (requirements §3)

| Extension point | Contract | Adding content means |
|---|---|---|
| `OrbConfig` | ScriptableObject | duplicate an asset, tune values |
| `OrbEffect` | abstract; `OnImpact(OrbShotContext, OrbImpactContext)` | one small subclass |
| `IOrbHitReceiver` | `void ReceiveOrbHit(OrbImpactContext)` | implement on your component |
| `IEnemyShotReceiver` | enemy-side damage intake | implement on your enemy |
| `IOrbShotRouter` | swap the shot delivery strategy | one class, assigned in the scene |

No content addition requires editing the table above.

## 6. Drift control

```
check-foundation-drift.ps1
  for each app, for each foundation file:
      hash(canonical) vs hash(app copy)
      ├─ equal        -> ok
      ├─ differs      -> DRIFT
      ├─ missing      -> MISSING
  for each app-only file in a foundation-owned folder:
      -> APP-ONLY (move to foundation)
  exit 1 if any finding

sync-foundation.ps1
  pre-flight the same comparison
      ├─ clean        -> copy everything
      └─ drift found  -> list it, exit 1 (no -Force) / overwrite (-Force)
```

The pre-flight is the important half: it turns "my fix disappeared" into a
message that names the file before anything is written.

## 7. Hook wiring (requirements §6)

| Hook | Trigger | Action | Guards against |
|---|---|---|---|
| Guard the two-app contract | `PostFileSave` `\.cs$` | `check-foundation-drift.ps1` | fixing the copy, losing it on sync |
| Compile gate | `PostFileSave` `\.cs$` | `compile-check.ps1` | shipping code that does not compile |
| Keep `.kiro` out of gitignore | `PostFileSave` `\.gitignore$` | `check-kiro-tracked.ps1` | disqualifying the submission |
| GUID integrity | `PostFileSave` `\.meta$` | `check-guid-integrity.ps1` | duplicate GUIDs breaking scene refs |
| Foundation reminder | `PromptSubmit` | agent prompt | designing an edit in the wrong tree |

Each hook command points at a script under `tools/`; `check-kiro-tracked.ps1`
verifies that correspondence so a hook cannot silently reference a missing file.

## 8. Testing strategy

- **Property-based** (`.kiro/specs/skeleton-foundation/correctness.md`) for the
  invariants that must hold for *any* input: fire-rate monotonicity, burst budget,
  contact ordering, drift symmetry.
- **Example-based** for scene wiring: does the range scene play, does the grove
  scene play.
- **Compile gate** in CI and as a Kiro hook, because a Unity project fails in a
  way that reads as a runtime bug when it is actually a compile error.
