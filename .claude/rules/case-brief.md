# The case brief — hard rules

Source: `Game Developer Case.pptx` from Apps (apps.com.tr), received 2026-09-01. Every
line below is a requirement from that deck, slide-numbered. **These rules are never
overridden** — not by `PLAN.md`, not by the portfolio phase plan, not by a juice idea, not
by an architectural preference — unless Salih explicitly says so in chat for that one
change. If any other file in this repo disagrees with this one, this one wins.

Deadline: **Monday 2026-09-07**, delivered by mail after the final push.

## Fixed numbers

| Fact | Value | Slide |
|---|---|---|
| Cube grid | **always 10x10** | 3 (Not1) |
| Slots | **always 5** | 3 (Not2) |
| Shooter columns | **level-driven, not fixed** | 3 (Not3) |
| Queue rows visible beyond the selectable row | **exactly 2** | 2, 3 (Not4) |
| Colours | **exactly 5: red, blue, yellow, orange, green** — all five in the sample level | 10 |
| Unity | **6000.0.68f1** | 11 |
| Render pipeline | **URP** | 11 |

## Scene layout (slide 2)

- Bottom: the shooter queue. Only the **top row is selectable**; rows below it are not.
- Middle: the five slots where selected shooters seat.
- Top: the 10x10 cube grid.

## Core mechanic (slides 4-6)

- Tap a shooter in the selectable row. Selectable shooters look "more stroked":
  `Cube_Outline` added as a **secondary material** on the object.
- A selected shooter runs to the **next empty slot**.
- A seated shooter fires at **front** cubes of its own colour. Cubes behind a destroyed
  cube **flow downward** (toward the shooter) to fill the gap.
- Ammo reaches zero → the shooter runs off the scene and **frees its slot**.
- Ammo left but no matching front cube → the shooter **stays put and keeps occupying its
  slot**.
- **Forbidden: the merge feature.** In the original, three same-colour shooters in the
  slots merge into one. The case explicitly does not want this. Every sent shooter holds
  a slot until its ammo is gone. Do not build merge, do not leave hooks for it.

## Mandatory feature: Hidden Shooter (slide 7)

- Some shooters have their colour concealed. They show no colour until they reach the
  **front (selectable) row**; on arriving there the colour is revealed.
- The sample level **must contain** at least one hidden shooter.

## Win / fail (slides 8-9)

- **Win:** every cube destroyed. Show: background darkens, the word **WIN** in front, plus
  a **restart button** that replays the same level.
- **Fail:** all 5 slots occupied and no shootable front cube left. Show: background
  darkens, the word **LOST** in front, plus a restart button that replays the same level.
- The brief says "restart the same level" after a win too. Salih chose NEXT-after-win as
  his own extension; if that is ever questioned the revert is two lines
  (`LevelProgression.Record` ignores Won, label stays RESTART).

## Level (slide 10)

- The delivered project **ships one sample level** already inside the game.
- Pressing **Play in the Editor boots that level directly**, playable with no further
  effort. No main menu, no start button (slide 11).
- The level is **data in a JSON file**, read and parsed at game start. The JSON's
  organisation is the candidate's choice.
- The sample level uses **all 5 colours**.
- The sample level is **consistent: solvable with correct moves**. The named failure to
  avoid: no red shooters left while red cubes still stand. (Enforced by
  `LevelFileTests`: ammo >= cubes per colour for every file in `Assets/00_GAME/Levels/`.)
- The sample level **contains the hidden shooter feature**.
- No design ambition is expected of the level: any level satisfying the points above and
  running consistently is enough.

## Delivery (slide 11)

- Game starts **directly in the level scene**. No menu, no start button.
- Testing is done **in the Editor**; no Android/iOS build is expected.
- **URP**, Unity **6000.0.68f1**.
- Delivered over **git**: regular commits with meaningful messages **from the start of
  development to the end**. Private repo of the candidate's own, with **`info@apps.com.tr`
  authorised from the moment the project is first opened**
  (`github.com/salihgireniz1/this_is_blast_replica`).
- On completion, **notify the contact by mail**.

## Evaluation order (slide 11) — most important first

1. **Bug-free.**
2. **Juiciness / fun factors** added to raise game feel.
3. **A recognisable architecture followed, and clean code.**
4. **Every performance optimisation made, even where it is not thought necessary.**
5. **Git usage and commit messages.**

When two concerns compete, the higher item on this list wins. A juice feature that risks a
bug is not added; an architectural nicety that risks a bug is not added.

## Art (slide 1)

- Required art (2D/3D, materials, animation, particles, etc.) is in
  `AppsAssets.unitypackage`, shipped next to the deck. Shooters and cubes come in
  per-colour versions there (slide 10). Use these assets.
- The deck's images are illustrative only; the specs on the later slides are the
  requirement.
- The deck recommends playing *This is Blast!* itself to understand the core mechanic
  and the hidden shooter feature.
