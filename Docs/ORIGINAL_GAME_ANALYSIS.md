# Reading the original game

The shipped *This is Blast!* build (`com.kiragan.blastjam` 3.20.0, Unity 6000.0.63f1, an
XAPK from APKPure) was opened and measured while building this case. The standing rule for
it: **read numbers, never take assets.** Configuration, material values, level schemas and
statistics are measurements, the same category as reading a pixel off a screenshot. Meshes,
textures, audio, code and level content do not enter this repository. The one exception,
a single level converted into our format, is documented below with every adaptation.

## How it was read

```
unzip <xapk>                       # base APK + asset packs
  UnityDataAssetPack.apk/assets/aa/Android/*.bundle    # Addressables: levels, materials
  com.kiragan.blastjam.apk/assets/bin/Data/data.unity3d # boot settings, shaders
```

`UnityPy` 1.25.3 parses the bundles. Two traps: its type schema is 4 bytes short for
6000.0.63, so every read needs `read_typetree(check_read=False)`; and material properties
come back as `(key, value)` tuples as well as dicts. Tools, all in `Docs/Tools/`:

| Tool | Does |
|---|---|
| `extract_levels.py` | Dumps every level ScriptableObject in the `levels` bundle to JSON, with an index CSV, an object map and a sha256 of the source. Output is gitignored on purpose. |
| `verify_extracted_levels.py` | Re-reads the bundle and checks the dump against it. |
| `convert_original_level.py` | Converts one dumped level into this project's JSON and proves it solvable under our rules (see *The converted level*). |

## What was measured and what it decided

| Measured | Value | Decision it fed |
|---|---|---|
| Colour space | Linear | The project is Linear; the clone prototype (gamma) is art direction only. |
| Cube tints and TCP2 ramp | `_Color` per colour, ramp 0.592 / 0.723, matcap on, shadows not received | `PaletteData` tints; the Toony Colors ramp; cubes cast but do not receive shadows. Full table in `.claude/notes/colour-space-and-original-game.md`. |
| Colour slots | 16, unit and shooter material sets identical in 12 | One palette serves cubes and shooters; `BlastColor` is a byte because colours get appended. |
| Board size | width always 10; heights 11-20 (1308 of 2447 levels are 10x20, one is 10x10) | The case's 10x10 keeps the original's width; depth is where the original scales difficulty. |
| Hidden shooter | a `SecretItem` flag on a deck cell | Our `hidden` flag, revealed on reaching the selectable row. |
| Ammo | not authored; derived at load (`ignoreExtraAmmoFromConfig`, `trimSurplusAmmo`) | We author ammo in the file so the parser can refuse an unwinnable level up front (`LevelFileTests`). |
| Level cohorts | 6 Addressables folders: Control Cohort 2000, Control Loop 332, FTUE Flow 51, D7 29, Bigger Boards 27, Early Churn Reduce 8 | Nothing to build; it shows the level set is an A/B surface (onboarding, day-7 retention, churn). |
| Post-processing | not established (URP or Built-in unreadable from the boot bundle) | Ours was measured instead: vignette + HDR cost 2 ms on the Galaxy A16 and were removed (`PERFORMANCE.md` 2f/2g). |

## Level schema, theirs and ours

The original's level is a ScriptableObject: `myStack` (the board) is a row-major list of
cells with 15 boolean mechanic flags each (`Block2x2`, `Gate*`, `Wall`, `Snake`, `Curtain*`,
`Key`, `Collectable`, `Pool`, ...), `myStage` (the deck) a column-major list with 13 more
(`Merge`, `Link*`, `Frozen`, `Lock`, ...), plus `difficultyLevel` and A/B `profiles`. It is a
wide table built for a live game with many mechanics and an in-editor painter.

Ours is a text file a designer can read: rows of colour letters, explicit ammo, `hidden`
per shooter. One parser is the trust boundary and every refusal names the location. The
case has one mechanic beyond the core, so a flag per mechanic per cell would be 99% zeros.

## The converted level

`convert_original_level.py` turned **Control Cohort level 18** into
`Level_Original_18.json`, whole: 10x20, 200 cubes, 55 shooters in 5 columns, 14 of them
hidden, the level the original itself marks `difficultyLevel` 2.

```
python Docs/Tools/convert_original_level.py --source "extracted_levels/levels/Control Cohort VO/18.json" \
    --map 2=Y,3=B,6=O,9=R,11=G --rows 20 --output Assets/00_GAME/Levels/Level_Original_18.json
```

What changed on the way in, and nothing else:

- **Colour names.** Their indices 2/3/6 are our Yellow/Blue/Orange; 9 (dark grey) and 11
  (brown) have no case colour and are written as Red and Green. The pattern is theirs.
- **Ammo,** which the original derives at load, is written exact-fit: each colour's cubes
  split over its shooters, front-most taking the remainder.
- **Orientation.** The file does not say which end faces the shooters. Row 0 read as the
  front is the level the designer meant: the winning line drains the five columns one after
  another, and the other reading collapses the oracle to 2%.

Measured, and the reason it ships whole:

| Player | Level_Original_18 | Level_01 (hand-authored) |
|---|---|---|
| Oracle, sees hidden shooters, replans every tap, random firing order | 100% | 100% |
| Blind random taps | 6% | 60% |
| Oracle driving the real game through `GameDirector` | won, 55 taps | wins |

The first attempt was level 21 cropped from 10x12 to the case's 10x10. It passed the same
simulator and then lost in the real game at tap 12: with exact-fit ammo, *which* same-colour
shooter fires a cube decides whether a slot frees, and cropping had broken the balance that
made the answer not matter. The original also leans on merge and surplus ammo, both
forbidden here. So a level is taken whole or not at all, `Level_01` stays hand-authored as
the sample the case asks for, and `Level_Original_18` is the stress and test level: point
the scope's `_level` at it (it is not in the boot path). It is also the largest board the
performance ledger's numbers were taken on, a 10x20 of the original's own making.

## Not taken

Meshes, textures, audio, animation, code, and the other 2446 levels. The extraction output
stays in a gitignored folder; only the tools, one level and this reading are in the repository.
