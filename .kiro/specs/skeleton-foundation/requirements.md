# Requirements — Skeleton Foundation for Two Applications

## Context

This spec defines the skeleton itself: the shared combat core in `foundation/`,
and the contract that lets two unrelated applications be built from it. It is the
Kiroween **Skeleton Crew** requirement — one lean template, demonstrated through
two distinct apps.

Terminology used throughout, in EARS form (`WHEN … THE system SHALL …`):

- **Foundation** — the source tree in `foundation/Assets/DuckGame/{Runtime,Shaders,Art}`.
- **App** — one standalone Unity project under `apps/<name>/unity`.
- **Content** — anything an app owns: scenes, prefabs, materials, configs, art.

---

## Requirement 1 — Two independent applications

**User story.** As a judge, I want to open either app on its own, so that a
"shared skeleton" is proven by two running games rather than by a diagram.

**Acceptance criteria**

1. WHEN a user opens `apps/training-range/unity` in Unity 2022.3.62f3c1 and
   presses Play on `Assets/DuckGame/Generated/v03/Playground.unity`, THE app
   SHALL enter play mode with a controllable player, a shoulder camera and a
   firing orb weapon.
2. WHEN a user opens `apps/expedition/unity` in Unity 2022.3.62f3c1 and presses
   Play on `Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity`, THE app
   SHALL enter play mode with a controllable player, enemies, a merchant and a
   HUD.
3. WHEN only one app's folder is present on disk, THE other app's absence SHALL
   NOT prevent the present app from opening or playing.
4. THE two apps SHALL differ in content and game design, not merely in scene name.
5. EACH app SHALL declare its entry scene in `ProjectSettings/EditorBuildSettings.asset`.

## Requirement 2 — The foundation is the whole combat core

**User story.** As a developer, I want the shooter to already feel correct, so
that my jam time goes into content instead of camera maths.

**Acceptance criteria**

1. THE foundation SHALL provide camera-relative locomotion with walk, sprint,
   jump and slide.
2. THE foundation SHALL provide a shoulder camera with a smooth aim-down-sights
   blend and obstacle handling.
3. THE foundation SHALL provide an orb weapon whose rate of fire is gated by a
   simulation-time limit, so a frame hitch cannot be exploited to fire faster.
4. WHEN the magazine is empty, THE weapon SHALL begin an automatic refill after
   a configurable delay and SHALL NOT exceed one shot per committed frame.
5. THE foundation SHALL deform the hat geometry from the real hat bone, so the
   charge visual and the muzzle position agree.
6. THE foundation SHALL resolve orb impacts with a sweep test, so a fast orb
   cannot tunnel through a thin collider.
7. THE foundation's scripts SHALL live in namespace `DuckGame` (or a child
   namespace of it) and SHALL NOT reference any app-specific content type.

## Requirement 3 — Content is data, not code

**User story.** As a team member, I want to add content without editing combat
code, so that two people can work without colliding in the same file.

**Acceptance criteria**

1. WHEN a new orb behaviour is needed, it SHALL be expressible as a new
   `OrbConfig` ScriptableObject asset plus, at most, a new `OrbEffect` subclass.
2. WHEN a new enemy is needed, it SHALL be expressible as a prefab plus tuning
   values, without editing the wave controller's spawn logic.
3. WHEN a new level is needed, it SHALL be a new scene under
   `Assets/DuckGame/Maps/Vnn/Generated/`, leaving earlier version folders intact.
4. IF adding content would require editing `foundation/Runtime`, THEN the change
   SHALL instead introduce a new extension point in the foundation and place the
   content in the app.

## Requirement 4 — Drift between copies is detectable and recoverable

**User story.** As a maintainer, I want to know when an app has silently diverged
from the foundation, so that a fix applied in the wrong place is not lost.

**Acceptance criteria**

1. WHEN `tools/check-foundation-drift.ps1` runs, THE tool SHALL hash-compare every
   foundation file against both app copies and report per-file drift.
2. WHEN `tools/sync-foundation.ps1` detects an app copy that differs from the
   foundation, THE tool SHALL refuse to overwrite and SHALL exit non-zero unless
   `-Force` is passed.
3. WHEN a file exists only inside an app copy of a foundation-owned folder, THE
   drift check SHALL report it as app-only so it can be moved to the foundation.

## Requirement 5 — Asset licensing is provable from the repo alone

**User story.** As a judge, I want to know every asset's licence, so that the
"open source" claim is verifiable rather than asserted.

**Acceptance criteria**

1. THE repository SHALL contain an OSI-approved licence that GitHub detects and
   shows in the About section.
2. WHEN an asset is redistributed in the repo, a licence file for it SHALL exist
   in that asset's folder.
3. `CREDITS.md` SHALL list every third-party asset with author, licence and
   location.
4. WHEN an asset's licence does not permit public-source redistribution, it SHALL
   NOT be committed.

## Requirement 6 — Kiro artefacts are present and tracked

**User story.** As a submitter, I want the Kiro usage to be inspectable, so the
judging criteria on Kiro usage can actually be evaluated.

**Acceptance criteria**

1. THE repository SHALL contain `.kiro/` at the root, with `steering/`, `specs/`,
   `hooks/` and `agents/` populated.
2. `.kiro` SHALL NOT appear in any `.gitignore`.
3. WHEN `tools/check-kiro-tracked.ps1` runs, it SHALL exit non-zero if `.kiro` is
   ignored, empty, or untracked.
4. EVERY hook in `.kiro/hooks/` SHALL reference a script that exists in `tools/`.
