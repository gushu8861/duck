# Third-party assets and licences

Project code is MIT (see `LICENSE`). Assets below keep their own licences.
Each entry names the folder that ships the original licence file.

## Redistributed in this repository

| Asset | Author | Licence | Location | Licence file |
|---|---|---|---|---|
| Goose Wizard rig (`GooseWizard_v02.fbx`) | authored for this project | project MIT | `foundation/Assets/DuckGame/Art/` | `SOURCE.txt` |
| Low Poly Vending Machine | Valentin Laffitte | **CC0 1.0** | `ThirdParty/ValentinLaffitteVending/` | `README.txt` |
| KayKit Halloween Bits — stone arch | Kay Lousberg (KayKit) | **CC0 1.0** | `ThirdParty/KayKitHalloweenArch/` | `LICENSE.txt` |
| Sci-Fi Sounds (Kenney) | Kenney | **CC0 1.0** | used inside `Maps/V09/Audio/` | `Kenney-SciFi-License.txt` |
| Interface Sounds (Kenney) | Kenney | **CC0 1.0** | used inside `Maps/V09/Audio/` | `Kenney-Interface-License.txt` |
| Feedback SFX (`Shot`, `Target`, `Environment`, `Wood`, `Ready`, `Purchase`, `Denied`) | original synthesis for this project, incorporating the Kenney samples above | project MIT + CC0 | `Maps/V09/Audio/` | `SOURCE.md` |

Links:

- Valentin Laffitte — <https://valentin-laffitte.itch.io/retro-vending-machine-3d-asset>
- KayKit Halloween Bits — <https://github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0>
- Kenney Sci-Fi Sounds — <https://kenney.nl/assets/sci-fi-sounds>
- Kenney Interface Sounds — <https://kenney.nl/assets/interface-sounds>
- CC0 1.0 — <https://creativecommons.org/publicdomain/zero/1.0/>

CC0 permits copying, modification, distribution and commercial use without
attribution. Attribution is given here anyway.

## Asset-provenance policy for this repository

The `apps/expedition` project carries an imported art and audio set under
`Assets/DuckGame/TSAssets/`, referenced by `Maps/V12/Generated/ExpeditionHub.unity`.
That set is **local prototype material** and is documented as such in
`apps/expedition/README.md`; it is not relicensed by this repository, and anyone
reusing this skeleton should supply their own art in its place.

Everything else in this repo is either authored here or CC0. If you fork this
project for a submission of your own, delete `TSAssets/` and point
`ExpeditionHub.unity` at your own assets before you publish.

## Engine and packages

Unity 2022.3.62f3c1 and the packages in `Packages/manifest.json` (URP,
TextMeshPro, Timeline, Visual Scripting, IDE integrations) are used under the
Unity Personal / Unity Terms of Service. No package is vendored into this repo;
`Packages/packages-lock.json` pins the resolved graph.
