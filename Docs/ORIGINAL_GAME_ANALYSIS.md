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
| Ammo | not per shooter: `requiredCount` = 20 for every shooter, confirmed on the HUD (520 = 26 x 20 on level 4) | We author ammo per shooter and exact-fit, so the parser can refuse an unwinnable level up front (`LevelFileTests`) and no shooter outlives its colour without merge. |
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

`convert_original_level.py` turned **Control Cohort level 4** into `Level_Original_04.json`,
whole: 10x12, 120 cubes in the original's own yellow, blue and red, so no colour was
renamed, 26 shooters in 5 columns. Checked against the running original side by side: the
deck matches row for row and the front row of cubes reads `YYBBYYRRYY` in both.

```
python Docs/Tools/convert_original_level.py --source "extracted_levels/levels/Control Cohort VO/4.json" \
    --map 2=Y,3=B,5=R --rows 12 --output Assets/00_GAME/Levels/Level_Original_04.json
```

The one thing that changed on the way in is **ammo**. The original gives every shooter
`requiredCount` = 20 (the 520 on its HUD is 26 x 20). With 72 yellow cubes over 16 yellow
shooters most of them would never empty and would hold their slots; the original's merge
feature absorbs that, and the case forbids merge. So the file carries exact-fit ammo: each
colour's cubes split over its shooters, front-most taking the remainder.

Two readings of the data were wrong at first, and the running game caught both: the board
is **column-major** like the deck (cell = column x height + row), and the file's **last row
faces the shooters**. Read row-major the board was noise that still happened to be
solvable; read the wrong way up, level 18 was solvable while the real level 18 is not.

| Player | Level_Original_04 | Level_01 (hand-authored) |
|---|---|---|
| Oracle, sees hidden shooters, replans every tap, random firing order | 100% | 100% |
| Blind random taps | 70% | 60% |
| Oracle driving the real game through `GameDirector` | won, 26 taps | wins |

## The dense levels, and why none ships

The original's hard levels were tried as test levels with the corrected reading. Levels 18
and 650 are unsolvable under our rules in the simulator. Level 868 (10x20, 50 shooters,
18 hidden) passes the simulator with a 100% oracle and **lost the real game at tap 12**,
three blue shooters seated with 1, 4 and 4 ammo and no line left; level 21 cropped to 10x10
had done the same earlier. The mechanism is the same each time: with exact-fit ammo,
*which* same-colour shooter fires a cube decides whether a slot frees, and in the real game
column locks and run-in timing settle that, not the simulator's slot order. The original
does not have the problem because 20 ammo per shooter plus merge frees slots by merging,
not by emptying.

So a level of theirs is consistent under our rules only when a blind player wins most of
the time, which their early levels do and their dense ones do not. Level 4 ships as the
test level (point the scope's `_level` at it; it is not in the boot path), the dense ones
do not, and `Level_01` stays hand-authored as the sample the case asks for. This is the
concrete form of the brief's consistency rule: a level authored for different rules is not
consistent under ours until it is measured, in the real game.

## Not taken

Meshes, textures, audio, animation, code, and the other 2446 levels. The extraction output
stays in a gitignored folder; only the tools, one level and this reading are in the repository.
