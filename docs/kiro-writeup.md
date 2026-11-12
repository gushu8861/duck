# How Kiro was used

Kiroween judges weigh *how* Kiro was used, not just that it was used. This is the
honest account: which surface, which feature, what it changed, and where it did
not help.

---

## 1. Surfaces

| Surface | What I used it for | How it differed |
|---|---|---|
| **Kiro IDE** | Almost everything: specs, steering, hooks, agent sessions, reading Unity YAML | The only surface where the `.kiro/` artefacts are first-class and editable in place |
| **Kiro CLI** | Headless runs of the verification scripts and repo-wide greps | No editor chrome, so it is faster for "run this and show me the errors" loops. Hooks from `~/.kiro/hooks` also fire here, which made it useful for testing the hook definitions themselves |
| **Kiro Web** | Reading and adjusting steering while away from the dev machine | Steering travelled with the repo; hooks do not execute on Web, so it was read-and-plan only |

**The difference that mattered:** hooks and specs are workspace-scoped artefacts.
Keeping the same `.kiro/` directory in the repo meant every surface saw the same
rules, and switching surfaces never meant re-explaining the project.

---

## 2. Steering — the highest-leverage thing here

Three files, and they changed the output more than any prompt I wrote.

### `structure.md` — killed the "helpful" refactor

Early on, every session that touched the repo wanted to introduce an assembly
definition per folder, or a UPM package for the shared code, or a scriptable
"framework". Each of those is defensible in isolation and each would have broken
every scene, because Unity binds scripts by GUID.

Writing down *why* the foundation is copied rather than packaged — with the scene
GUID consequence spelled out — is the single change that stopped Kiro from
re-proposing the package layout. Once the reason was in steering, the suggestion
disappeared from every later session.

### `tech.md` — stopped the second input system

Without a pinned input story, sessions alternated between `Input.GetAxis` and the
new Input System package, and half a feature would land against the one that was
not installed. Pinning "classic `Input`, centralised so this is a one-file change"
made every later session consistent.

### `product.md` — made the non-goals real

The "Skeleton Crew" category asks for a template that is *lean*. Lean is a
judgement, and without it written down, scope crept toward a full game. Writing
the non-goals as bullets ("not a finished game, no save system, no store build
pipeline") gave a concrete thing to point at when a session proposed exactly those.

**Strategy that made the biggest difference:** steering files that record
*decisions with their consequences*, not just conventions. "Use URP 14" is a
convention; "copied, because packaging breaks scene GUID binding" is a decision
that prevents a whole class of wrong turns.

---

## 3. Spec-driven development

### How the spec was structured

`skeleton-foundation` was written as three files in the order Kiro expects:

1. **`requirements.md`** — six numbered requirements, each with a user story and
   acceptance criteria in EARS form (`WHEN … THE system SHALL …`). The EARS form is
   not decoration: it forced "the app SHALL play" instead of "the app should be
   playable", which is a testable claim.
2. **`design.md`** — the two decisions that carry everything (copied foundation,
   ScriptableObject extension points), the module boundary table, and the firing
   pipeline drawn as the actual gate → context → router → sweep flow.
3. **`tasks.md`** — seven phases with checkboxes, including a "deliberately out of
   scope" phase so that *not* doing something is recorded as a decision.

### What the spec-driven approach actually bought

The measurable difference was in **ordering**. Writing `requirements.md` §2 before
touching code meant the foundation boundary was defined by *what the skeleton must
provide*, not by *which files happened to be next to each other*. I walked the
scene GUID graph to find candidate shared code — it returned 11 files — but the
spec said the foundation must provide rate-gated firing and sweep-test impacts,
which are script-to-script dependencies the scene graph cannot see. That gap is
exactly what broke the first headless compile (`OrbShooter` without `OrbMagazine`).

### Spec vs. vibe coding, concretely

| | Vibe coding | Spec-driven here |
|---|---|---|
| Boundary decision | "these files are near each other" | "these are the modules the requirements name" |
| First compile | fails on missing `OrbMagazine`, `IOrbShotRouter` | failed on the same thing, but the spec *explained why* and pointed at the fix |
| Scope | grew toward save systems and menus | held by an explicit non-goals phase |
| Handoff to future me | lost in chat | three files I could re-read |

The honest version: spec-driven did not prevent the compile error. It made the
compile error *diagnosable in one step* instead of exploratory.

---

## 4. Agent hooks

Five hooks, all in `.kiro/hooks/project-guards.json`, each targeting a failure that
had actually happened:

### `PostFileSave` → compile gate (`\.cs$`)

The most valuable one. A Unity project that fails to compile shows **nothing** in
the console a human looks at — just "All compiler errors have to be fixed". A
newcomer reads that as a runtime bug. Running `tools/compile-check.ps1` after
every C# save turned a mystery into a named error.

It paid for itself on the first run: `CS0101` duplicate-type errors revealed that a
re-assembly had layered a second copy of the code over the first. Without the hook
that would have surfaced much later as "the scene silently lost its references".

### `PostFileSave` → foundation drift (`\.cs$`)

The failure mode this prevents is the nastiest kind: you fix something in an app's
copy of a foundation file, it works, you commit, and the next sync erases it with no
error. The hook hash-compares and names the file.

### `PostFileSave` → `.kiro` must stay tracked (`\.gitignore$`)

A one-line `.gitignore` entry is enough to disqualify a Kiroween submission. This
is exactly the kind of thing a tired person adds at 2 a.m. ("don't commit the agent
folder"). The hook re-checks on every `.gitignore` save.

### `PostFileSave` → GUID integrity (`\.meta$`)

Scenes bind by GUID. A duplicate GUID silently makes one asset win. Cheap to check,
expensive to debug.

### `PromptSubmit` → foundation reminder (agent hook)

The only agent-action hook. It restates the two-app rule as the agent starts
designing. This is the hook that most resembles "steering at the moment of
relevance": the full rule lives in steering, but the agent hook fires exactly when
a plan is being formed, which is when the rule is easy to violate.

**What hooks did for the process:** they converted "I should remember to…" into "it
is impossible to forget", for five specific things. That is the difference between
a checklist and a guardrail.

---

## 5. Custom agents

Two, both in `.kiro/agents/`:

### `skeleton-guardian`

Resources load the steering docs and the foundation runtime, tools restricted to
read + shell, and a prompt that forces a three-way decision before any edit:
combat core → foundation, content → app, content needing a core change → **add an
extension point first**.

The prompt's job is to stop the most common wrong edit. The interesting part is the
third branch: without it, the agent's instinct is to widen the core to fit the
content, which is how a skeleton stops being a skeleton.

### `unity-verifier`

Restricted to read + shell, no write. Its prompt encodes the log-reading lessons
that cost the most time:

- batchmode **exits 0 even when compilation fails** — never trust the exit code,
  grep for `error CS`
- `CS0101` means a duplicate copy, not a logic bug
- "type not found" for a type that exists on disk means a missing `.meta`

Isolating verification into an agent with no write permission was deliberate: the
agent that judges whether the build works should not be able to "fix" it while
judging.

---

## 6. Property-based testing

`.kiro/specs/skeleton-foundation/correctness.md` holds eight properties translated
from the EARS requirements. The three that earned their keep:

1. **Refill never overfills.** PBT found that a refill tick larger than the reserve
   overfilled the magazine by one, because the branch assumed a full tick.
2. **Burst budget conservation.** A burst consumed one orb *per frame* rather than
   per *shot*, so a burst of three cost three orbs at 30 fps and one at 120 fps.
   No hand-written test at a fixed frame rate would have found it.
3. **Drift check symmetry.** The drift tool ignored `.meta` files — the exact file
   whose corruption breaks a scene — so a mutated `.meta` reported clean.

Property 7 is the meta-test worth calling out: *adding an `OrbConfig` must not
change `foundation/Runtime`*. That turns the skeleton's central claim into a hash
comparison. If the claim is a fiction, the hash changes.

---

## 7. Where Kiro did not help

Being specific about the limits, because a write-up that claims everything worked
is not useful:

- **Unity YAML.** Scene and prefab files are gigantic generated YAML. Reading them
  was useful; *editing* them through an agent was not, because a plausible-looking
  edit produces a scene that opens with missing references. The working method was
  always: change the C# or the prefab source, let Unity re-serialise.
- **GUID reasoning.** The agent can follow a GUID graph if asked explicitly, but it
  does not spontaneously notice that a scene's dependency closure and a script's
  dependency closure are different sets. Writing that distinction into the design
  doc is what made it reliable.
- **Model-choice guessing.** Earlier sessions invented asset sources. Pinning the
  licensing policy in `tech.md` ("a licence file must land beside the asset") made
  every later suggestion checkable rather than plausible.

---

## 8. Summary

The leverage was not in generating code faster. It was in three things:

1. **Steering that records decisions with consequences** — stopped entire classes of
   wrong turns (packaging the foundation, swapping input systems, widening scope).
2. **Hooks that target failures that already happened** — five guards, each from a
   real incident, converting memory into mechanism.
3. **A spec that made the boundary testable** — the GUID-graph gap between "what
   scenes reference" and "what the skeleton must provide" was visible in the
   requirements before it became a compile error.
