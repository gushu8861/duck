# Correctness — property-based tests for the firing pipeline

Kiro's spec-correctness translates EARS requirements into executable properties
and generates inputs to try to violate them. This file holds the properties for
the invariants in `requirements.md` §2 and §3. Each property is stated in EARS
form first, then as the invariant a test must check.

These are the properties where a single hand-written example is most likely to
lie, because the failure only appears under a specific frame timing, a specific
burst count, or a specific collider thickness.

---

## P1 — Fire-rate monotonicity (requirements §2.3)

> WHEN a player holds the fire input for any duration D over any sequence of frame
> times, THE number of committed shots SHALL NOT exceed `floor(D / minInterval) + 1`.

**Why it is a property.** A frame-hitch bug (`if (Time.time - last > rate)`) passes
any test that fires on a steady 60 fps loop and fails the moment one frame takes
200 ms. The interesting inputs are the irregular ones.

**Generator.** `D` uniform in `[0, 30]` s; frame deltas drawn from a mixed
distribution — 90% uniform in `[8, 20]` ms, 10% uniform in `[50, 400]` ms.

**Invariant.** `committedShots <= floor(D / config.minShotInterval) + 1`, and no
two committed shots share the same simulation timestamp.

---

## P2 — One shot per committed frame (requirements §2.4)

> WHEN any single frame's delta time is applied, THE weapon SHALL commit at most
> one shot, regardless of how much accumulated budget exists.

**Why it is a property.** A player who alt-tabs and returns gets an enormous
`deltaTime`. A budget-based implementation fires the entire magazine in one frame.

**Generator.** `delta` uniform in `[0.001, 5.0]` s, plus the exact boundary values
`0.0`, `1/60`, `1.0`, `5.0`.

**Invariant.** `shotsThisFrame <= 1` for every frame, and the magazine's consumed
count after a 5 s frame equals the consumed count after a 16 ms frame.

---

## P3 — Burst budget conservation (requirements §2.4, §3.1)

> WHEN a shot fires K orbs for any `K` in `[1, config.burstCount]`, THE magazine
> SHALL consume exactly min(K, remaining) orbs and SHALL spawn exactly that many
> projectiles.

**Why it is a property.** Off-by-one in burst accounting is invisible until the
magazine is nearly empty, which is exactly when a player notices.

**Generator.** `K` uniform integer in `[1, 8]`; `remaining` uniform integer
`[0, 12]`; `reserve` uniform integer `[0, 30]`.

**Invariant.** `spawned == consumed` and `remainingBefore - consumed == remainingAfter`,
with `remainingAfter >= 0`.

---

## P4 — Refill never overfills (requirements §2.4)

> WHEN a refill tick runs against any (load, reserve, capacity) triple, THE load
> SHALL NOT exceed capacity and the reserve SHALL NOT go negative.

**Generator.** `load` in `[0, capacity]`, `reserve` in `[0, 2*capacity]`,
`tickAmount` in `[1, capacity + 2]`.

**Invariant.** `0 <= load' <= capacity` and `reserve' >= 0` and
`load' + reserve' <= load + reserve`.

---

## P5 — No tunnelling (requirements §2.6)

> WHEN a projectile moves more than its own diameter in one step toward any
> collider of thickness `t > 0`, THE impact SHALL still be detected.

**Why it is a property.** This is *the* classic that unit tests miss: a hand-picked
"fast orb hits wall" test uses a thick wall. The bug needs a thin target and a fast
step.

**Generator.** Step length `L` uniform in `[0.01, 50]` m; collider thickness `t`
uniform in `[0.001, 1]` m; approach angle uniform on the sphere.

**Invariant.** `hitDetected == (segmentFromPrevToCurrentIntersectsCollider)`.
Discretising to point-sampling at the current position fails this for
`L >> t`, which is the whole point.

---

## P6 — Muzzle and charge visual agree (requirements §2.5)

> WHEN the hat deformation produces any bulge amount `b` in `[0, 1]`, THE muzzle
> position SHALL equal the hat-tip position computed from the same bone state.

**Generator.** `b` uniform in `[0, 1]`; bone rotation uniform; hat scale uniform in
`[0.5, 2]`.

**Invariant.** `|muzzlePos - hatTipPos(b)| < epsilon` for every sampled state.
Any implementation that derives the muzzle from a fixed transform offset fails
as soon as the hat scale or bulge differs from the reference pose.

---

## P7 — Content addition does not touch the core (requirements §3.1–§3.4)

> WHEN a new `OrbConfig` asset with any valid field values is added, THE firing
> pipeline SHALL run without editing any file under `foundation/Runtime`.

**Why it is a property.** "Add a new orb type" is the claim the whole skeleton
rests on. It is testable: hash the `foundation/Runtime` tree before and after
adding a config, and assert equality. If it changes, the extension point is a
fiction.

**Generator.** randomised `OrbConfig` field values across the documented valid
ranges (rate, burst, spread, speed, lifetime, gravity, radius).

**Invariant.** `hash(foundation/Runtime) unchanged` and the new asset fires.

---

## P8 — Drift check is symmetric (requirements §4.1)

> WHEN `check-foundation-drift.ps1` runs against two apps that both match the
> foundation, THE tool SHALL report zero findings; WHEN either copy is modified by
> any single byte, THE tool SHALL report exactly that file.

**Generator.** Pick a random file from the foundation, apply a random one-byte
mutation to one app's copy, run the check.

**Invariant.** `findings.Count == 1` and `findings[0]` names the mutated file and
the correct app. A tool that reports "drift somewhere" is not enough to act on.

---

## What PBT caught that examples did not

Recorded as the honest value of this exercise:

1. A budget-based refill that overfilled the magazine by one when
   `reserve < tickAmount`, because the recharge branch assumed a full tick.
2. A burst implementation that consumed one orb per *frame* rather than per
   *shot*, so a burst of three at 30 fps cost three orbs but at 120 fps cost one.
3. The drift check initially ignored `.meta` files, so a mutated `.meta` (the exact
   thing that breaks a scene) reported clean.
