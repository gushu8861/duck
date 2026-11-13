# Kiroween 2026 — trailer (v3)

`Duck-Kiroween-trailer.mp4` — **38.2 s**, 1920×1080, 30 fps, H.264 + AAC,
15.4 MB. English primary, Chinese as a smaller translation line.

Not uploaded yet.

---

## Why this version exists

The v2 cut had three problems, all structural rather than cosmetic:

1. **Every shot used the same move** — one slow `zoompan` push-in per still, held for
   4-9 s. Watching the same gesture repeated twelve times is what made it feel dull.
2. **The image shimmered.** `zoompan` computes a fractional crop origin and rounds
   it; at slow zoom the rounding flips between two neighbouring pixels every frame,
   which reads as a nervous jitter, especially on flat-coloured low-poly art.
3. **It was static.** Still images held for seconds with no internal change, which
   is a slideshow regardless of how nice the push is.

## What changed

### Tooling

Installed two open-source projects locally (in `vidtools/`, not in the repo):

- **Remotion** (`remotion-dev/remotion`) — renders each frame with a real browser
  engine, so motion uses CSS transforms, springs and real easing. This is what
  removed both the "same zoom every shot" limitation and the shimmer: transforms
  are sub-pixel and GPU-composited, not filter arithmetic on a rounded crop.
- **editly** was tried first and removed: it depends on `canvas`/`gl` native
  modules that need a node-gyp toolchain, and failing fast beat fighting the
  build.

### Motion

- **Gameplay is now real frame sequences, not stills.** The repo already contained
  ordered capture runs — hat charge at LOAD 0/45/87/ready/shot, and walk → turn →
  stop → takeoff → apex → landing. Playing them in order is actual motion; no
  synthetic camera needed. This is the single biggest change: five of eleven
  chapters are now genuine animation.
- **Cuts are ~2-3 s**, down from 4-9 s.
- **Each chapter type gets a different treatment**: gameplay = frame sequence,
  cards = spring-driven reveal with staggered elements, code = line-by-line reveal,
  architecture = elements arriving in dependency order with arrows that grow.
- **Punctuation**: a brightness flash on cut-ins, radial speed lines on the shot
  frame, and a short "MIT · OPEN SOURCE" beat before the end card.

### Audio

`public/music.wav` — synthesised, 38.2 s exactly, retimed to the new length
(chords 6.4 s, arpeggio from 8.5 s, shimmer 20 s, kick 26 s, outro drop 35 s).
Mean −21.8 dB, peak −4.3 dB: sits under the captions instead of competing.

### Fixed along the way

- Caption plate let the game's own HUD text (`READY 01 LOAD 0%`) ghost through the
  Chinese line. Now a fully solid slab with a soft ramp above it.
- Composition length was hard-coded at 2160 frames while chapters summed to 1146,
  producing 34 s of black. Duration is now derived from the chapter list.
- Code reveal used `frame % speed` for the newest line, which made it strobe; each
  line now fades in once and settles.

---

## Shot list (38.2 s)

| Time | Picture | Motion | Caption |
|---|---|---|---|
| 0:00–0:03.4 | Title | rule draws, kicker slides, "Duck" springs up, sub + meta fade | — |
| 0:03.4–0:06.2 | Grove Camp | eased pull-back + drift, flash on cut | Two apps. One combat core. |
| 0:06.2–0:09.4 | **Hat charge, 5 real frames** | frame sequence, impact lines on the shot | The hat physically charges, then fires |
| 0:09.4–0:12.2 | **Movement, 6 real frames** | frame sequence | Movement, jump and slide |
| 0:12.2–0:15.0 | **ADS + hit, 3 real frames** | frame sequence, flash | Aim down sights: orbs land where the reticle is |
| 0:15.0–0:18.4 | `OrbShooter.cs` | line-by-line reveal | Rate is gated in simulation time, not frames |
| 0:18.4–0:21.4 | Architecture | boxes spring in, arrows grow, chips stagger | (card carries the text) |
| 0:21.4–0:24.0 | Vending machine | push-in + drift | Buy upgrades with the coins your kills earn |
| 0:24.0–0:26.6 | Reward choice | push-in | Three choices, one core |
| 0:26.6–0:29.4 | Merchant shop | pull-back | Grove Camp: a merchant economy on the same core |
| 0:29.4–0:32.4 | **Slide / movement, 4 real frames** | frame sequence | Content is data, not code |
| 0:32.4–0:33.6 | Flash beat | white hit | MIT · OPEN SOURCE |
| 0:33.6–0:38.2 | End card | URL, rule, three Kiro rows stagger in | — |

All gameplay frames are real captures from the project. Code and architecture text
matches the repository.

---

## Regenerating

```powershell
$node = "<node>"
$w    = "<this workspace>\vidtools"

# preview one frame
& $node $w\node_modules\@remotion\cli\remotion-cli.js still index.ts Trailer out/f.png --frame=195

# full render
& $node $w\node_modules\@remotion\cli\remotion-cli.js render index.ts Trailer out/trailer.mp4 --concurrency=4

# music (regenerates at the duration set in make_music.py DUR)
& $py $w\make_music.py $w\public\music.wav
```

Edit `CHAPTERS` in `Trailer.tsx` to retime; the composition length follows it.
Assets live in `vidtools/public/img/`.

### Adding more motion

The best source of new material is more capture runs. `docs/verification/v04-play-final/`
and `v03-play/` hold ordered frame sets that were captured for regression testing;
any of them can be dropped into `public/img/` and passed to `<Gameplay frames={[...]}/>`
to become a real animated beat.

---

## Upload details

**Title**

```
Duck — one combat core, two standalone Unity apps | Kiroween 2026 (Skeleton Crew)
```

**Description**

```
Two standalone Unity 2022.3 projects built from a single shared combat-core
skeleton: a bare training range that isolates aim-and-fire feel, and a grove camp
that carries the same core with a merchant economy, enemies, a HUD and a full art
pass. Neither app needs the other.

Shared code lives in foundation/ and is copied into each app rather than packaged,
because Unity binds scenes to scripts by GUID — a package that moves breaks every
scene that referenced it. That trade (drift between copies) is paid for with
tools/check-foundation-drift.ps1 and tools/sync-foundation.ps1 instead of with the
first five minutes of whoever clones the repo.

Kiro usage, specifically:
• steering — decisions recorded together with their consequences. Writing down
  "copied, because packaging breaks scene GUID binding" stopped the recurring
  "let's extract the foundation into a package" refactor in every later session.
• hooks — five guards, each targeting a failure that had actually happened:
  compile gate, drift guard, .kiro-must-stay-tracked (a one-line .gitignore entry
  can disqualify a Kiroween submission), GUID integrity, and a PromptSubmit
  reminder that foundation edits must reach both apps.
• spec — the skeleton boundary stated as testable properties, including
  "adding an OrbConfig must not change foundation/Runtime", which is a hash
  comparison rather than an adjective.

Code: https://github.com/gushu8861/duck  (MIT)

Chapters
0:00  intro
0:03  the two apps
0:06  the orb weapon, hat charge
0:09  movement
0:12  aiming and hits
0:15  the firing code
0:18  architecture
0:21  the camp economy
0:29  content is data
0:33  repo and credits
```

**Tags**

```
Kiroween, Kiro, Unity, URP, game development, hackathon, skeleton crew, C#
```

**Devpost category:** Skeleton Crew

---

## Trade-offs

- **No in-engine capture.** Recording real footage means launching Unity, which
  competes with the other job on this machine. Frame runs from the existing
  regression captures are the substitute, and they are genuine motion rather than
  faked camera moves.
- **No voice-over.** Captions carry everything, so it works muted on autoplay.
- **English primary** because the panel is international; the Chinese line is a
  translation. Set the `zh` strings to `""` to drop it.
