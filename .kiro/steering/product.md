# Product Overview

## What this is

**Duck — A Kiroween skeleton for a magic-orb shooter.** Two playable Unity builds
that share one lean foundation: a third-person magic-orb combat loop with a
player-driven hat arsenal, built so a game-jam team can drop in new content
without touching the combat core.

The project is submitted to **Kiroween 2026** in the **Skeleton Crew** category,
which asks for a skeleton template lean enough to stay clear but flexible enough
to support several applications — demonstrated with **two distinct apps** built
from the same foundation.

## The two applications

| App | Folder | What a player does | Why it exists |
|---|---|---|---|
| **Training Range** | `apps/training-range` | Stands in a bare court, aims a shouldered camera, fires orbs from the hat, watches the magazine refill, tests ADS and movement. | The smallest honest slice: prove that aim, fire, reload and camera feel good before any content exists. |
| **Grove Camp** | `apps/expedition` | Walks a stylised grove, meets a snail merchant, buys orbs-upgrades with coins earned by hitting targets, fights forest enemies that charge and flee. | The same combat core carrying progression: merchant economy, enemy roles, hit feedback and layered toon art. |

Both apps are standalone Unity projects that open and run on their own. Neither
one needs the other. The shared part is code and shaders, not a shared binary.

## The foundation

`foundation/` holds the parts that both apps import:

- **Locomotion** — camera-relative movement with run, jump and slide, plus the
  player rig binding.
- **Shoulder camera** — spring-arm shoulder rig with a smooth ADS blend and
  collision handling.
- **Orb weapon** — `OrbConfig` (a ScriptableObject), `OrbShooter` (fire gate,
  rate limit, burst count), `OrbMagazine` (load/reserve state machine) and
  `OrbProjectile` (sweep-cast hit resolution).
- **Hat bulge** — the hat's local deformation driven by the real bone, so the
  charge visual and the muzzle position can never disagree.
- **Toon shaders** — URP 14 toon plus a dedicated outline pass.

The rule the skeleton enforces: **content is data, not code**. A new orb type is
an `OrbConfig` asset. A new enemy is a prefab plus a tuning struct. A new level is
a scene that references foundation prefabs. If a change would require editing the
combat core to add content, the skeleton has failed.

## Who it is for

Small teams and solo developers who want a shooter prototype that already feels
right to control, and who would rather spend the jam on art, levels and enemies
than on re-deriving a shoulder camera for the fourth time.

## Non-goals

- Not a finished game. There is no save system, no menu flow, no build pipeline
  for stores.
- Not a general-purpose engine abstraction. The foundation is deliberately
  opinionated toward one genre.
- No third-party game assets are redistributed. Every imported asset carries its
  own license file next to it.
