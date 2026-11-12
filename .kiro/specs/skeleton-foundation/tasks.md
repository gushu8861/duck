# Tasks — Skeleton Foundation

Status legend: `[x]` done · `[ ]` pending

## Phase 1 — Foundation extraction

- [x] 1.1 Identify the shared combat core by walking the GUID graph of both entry scenes and intersecting the results
- [x] 1.2 Move locomotion, camera, orb weapon, hat deformation into `foundation/Assets/DuckGame/Runtime`
- [x] 1.3 Move toon and outline shaders into `foundation/Shaders`
- [x] 1.4 Move the goose rig into `foundation/Art`, keeping `SOURCE.txt` beside it
- [x] 1.5 Confirm no foundation script references an app content type
- [x] 1.6 Create `apps/training-range/unity` with `Packages` and `ProjectSettings`
- [x] 1.7 Create `apps/expedition/unity` with `Packages` and `ProjectSettings`
- [x] 1.8 Assemble each app's `Assets` tree from its entry scene's dependency closure

## Phase 2 — Make the apps actually independent

- [x] 2.1 Copy the full code layer into the expedition app (closed over script-to-script references, not just scene references)
- [x] 2.2 Copy the foundation code layer plus V04 movement into the training app
- [x] 2.3 Add `Assets/Settings` URP assets to both apps
- [x] 2.4 Add `Assets/Scenes/SampleScene.unity` to both apps so the project boots clean
- [x] 2.5 Set each app's entry scene in `EditorBuildSettings.asset`
- [x] 2.6 Restore every folder `.meta` so GUIDs stay stable

## Phase 3 — Drift tooling

- [x] 3.1 Write `tools/sync-foundation.ps1` with a pre-flight drift check and `-Force` override
- [x] 3.2 Write `tools/check-foundation-drift.ps1` with hash comparison and app-only reporting
- [x] 3.3 Make the drift check exit non-zero so a hook or CI can gate on it

## Phase 4 — Kiro wiring

- [x] 4.1 Write `.kiro/steering/product.md`
- [x] 4.2 Write `.kiro/steering/tech.md`
- [x] 4.3 Write `.kiro/steering/structure.md`
- [x] 4.4 Write this spec: `requirements.md`, `design.md`, `tasks.md`
- [x] 4.5 Write `.kiro/specs/skeleton-foundation/correctness.md` (property-based tests)
- [x] 4.6 Write `.kiro/hooks/project-guards.json` with five hooks
- [x] 4.7 Write the four hook target scripts under `tools/`
- [x] 4.8 Add a `PromptSubmit` agent hook that restates the two-app rule

## Phase 5 — Compile and licence gates

- [x] 5.1 Write `tools/compile-check.ps1` (headless, both apps, unique CS errors only)
- [x] 5.2 Compile `apps/expedition` headless — zero CS errors
- [ ] 5.3 Compile `apps/training-range` headless — last run clean after the code-layer copy; re-run to record a log
- [x] 5.4 Write `tools/check-guid-integrity.ps1`
- [x] 5.5 Write `tools/check-kiro-tracked.ps1` (asserts `.kiro` is present, populated, untracked-by-nothing)
- [x] 5.6 Write `CREDITS.md` with every third-party asset, author and licence
- [x] 5.7 Ship `LICENSE` (MIT) so GitHub surfaces it in the About section

## Phase 6 — Submission surface

- [ ] 6.1 Record the demo video (< 3 min, public on YouTube, shows both apps running)
- [ ] 6.2 Build and publish the WebGL targets, then put the playable URL in the form
  - [x] 6.2a Write the missing `DuckGame.EditorTools.WebGLBuilder` build entry point (both apps)
  - [x] 6.2b Register the real entry scenes in each app's `EditorBuildSettings.asset`
  - [x] 6.2c `tools/prepare-webgl.ps1` — read-only pre-flight, no Unity process started
  - [x] 6.2d `tools/build-webgl.ps1` — headless build, treats non-empty `error CS` as failure
  - [x] 6.2e `tools/publish-webgl.ps1` — Pages landing page, `.nojekyll`, optional push
  - [ ] 6.2f Run the build on an idle machine, then enable Pages (branch `main`, folder `/`)
- [x] 6.3 Add the two screenshots referenced from the README
- [x] 6.4 Write `docs/kiro-writeup.md`
- [ ] 6.5 Fill the Devpost form: category Skeleton Crew, two app folders, Kiro write-up

## Phase 7 — Deliberately out of scope

- [ ] 7.1 Remove the imported prototype art set from `apps/expedition` and replace with first-party art (documented in `CREDITS.md` as local prototype material)
- [ ] 7.2 Save system and persistent unlocks
- [ ] 7.3 Menu flow and settings screen
- [ ] 7.4 Second enemy biome

---

## Notes on the order

Phase 1 had to come before Phase 4: the steering docs describe a structure, and
writing them first meant rewriting them once the closure walk taught me where the
real boundary was. Phase 5.2 was the single most useful checkpoint — a Unity
project that fails to compile reads like a runtime bug (nothing plays, no error in
the console view a judge looks at), so the compile gate earned its place in the
hooks.
