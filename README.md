# This Is Blast — case study

A from-scratch build of the *This is Blast!* core loop for the Apps game developer case:
a 10x10 cube grid, five slots, a shooter queue whose front row is tappable, hidden
shooters, WIN / LOST overlays with a restart, one JSON-driven sample level, no merge.

Unity **6000.0.68f1**, URP. Open `Assets/00_GAME/Scenes/Game_Scene.unity` and press Play;
the sample level boots directly. There is no menu.

## Tests

| Suite | Count | Run |
|---|---|---|
| Unity EditMode (`Blast.Tests.EditMode`) | 87 | Test Runner window, or `unity cmd run_tests --mode editor --filter "Blast.Tests" --filter_type assembly --async_tests true` |
| Level editor (`level-editor/logic.js`) | 41 | `node --test level-editor/logic.test.js` |

Every rule of the game has a test. Two of the tests guard things that break silently:
`ArchitectureTests` reads the `.asmdef` files on disk and fails when a layer references a
layer above it, and `LevelFileTests` parses every level in `Assets/00_GAME/Levels/` and
fails when a colour has fewer shots than cubes, which is the unwinnable level the brief
warns about.

## Architecture

Six assemblies. The dependency direction is enforced by the asmdef references, not by
convention, and the test above keeps it that way.

```
Blast.Domain          pure C#: BoardModel, ShooterQueue, SlotRow, GameRules (noEngineReferences)
  ^
Blast.Application     GameLoop, the one use case: TrySelect / TryShoot, verdict, column states
  ^            ^              ^
Presentation   UI (MVVM, R3)  Infrastructure (LevelParser, PaletteData)
  ^
Blast.Bootstrap       GameLifetimeScope: the VContainer composition root
```

`Blast.Diagnostics` (PerfProbe, PerfSweep) sits beside these, references none of them,
and only runs in development builds.

**Domain** knows nothing about Unity. Nothing in it moves: the board keeps its authored
colours and a per-column front index walks backwards through them; the queue does the
same forwards. A slot is occupied exactly while its shooter has ammo, so "free slot" and
"out of ammo" cannot disagree. `GameRules` is three pure functions: leftmost matching
front, won, failed.

**Application** is `GameLoop`. One call trades one shot for one cube and re-reads the
verdict in the same call, so the overlay can never be a move late. It asks *won* before
*failed*, because an emptied board with a full slot row satisfies both. It has no notion
of time; Presentation paces it.

**Presentation** dresses the loop's answers. `GameDirector` turns a tap into `TrySelect`
and runs each seated shooter's fire loop with UniTask; `LevelSpawner` owns every world
position and hands out views in the order the domain removed cubes; the views themselves
are humble, they apply what they are told and decide nothing. The column-state handshake
(`LockColumn` / `MarkSettling` / `MarkSettled`) exists because the domain removes a cube
the instant it is shot, while the bullet is still in the air on screen.

**UI** is one MVVM pair on R3 streams. **Infrastructure** is the trust boundary: the
parser refuses every malformed file with a `FormatException` that names the location.

**Bootstrap** registers the parsed models, `GameLoop`, the palette adapter behind
Presentation's `IColorMaterials`, and the scene components; VContainer builds the graph
and calls each component's `[Inject] Construct`. What *restart* means is decided here, in
a `SceneRestarter` entry point.

## The level file

```json
{
  "boardLayers": [ { "rows": ["RRRRRBBBBB", "..."] } ],
  "slotCount": 5,
  "shooterColumns": [ { "shooters": [ { "color": "R", "ammo": 10, "hidden": false } ] } ]
}
```

Rows are listed front row first, one letter per column (`Y R B G O`). Each shooter column
is listed front first. `hidden` conceals the colour until the shooter reaches the front
row. The shipped level is `Assets/00_GAME/Levels/Level_01.json`; the others are stress
levels used for the performance work. `level-editor/index.html` (no dependencies,
double-click) paints a board, autofills a queue and runs the same checks as the tests.

## Performance

Target: 120 FPS, 0 B of GC allocation per frame during play, worst case 360 cubes.
Measured on a Samsung Galaxy A16 over adb with `PerfProbe`, not in the Editor. The
ledger with every step's before and after is `Docs/PERFORMANCE.md`. The shipped level
runs at the phone's refresh rate with 0 B/frame idle and about 11 B/frame during a burst.
The things that made the difference: one reused tween per moving view instead of a
DOTween shortcut per move, a shared pool of collapse tweens, pooled bullets and splashes,
a fixed ring of audio voices, `sharedMaterial` everywhere.

## Decisions, including what was left out

- **No merge.** The brief forbids it; there are no hooks for it either.
- **No save system.** One level replays after a win as after a loss, so a stored index
  would never move.
- **Cubes are instantiated once and destroyed on death, not pooled.** Measured: pooling
  them changed nothing on the device, so it was not taken.
- **`GameDirector` has no unit tests.** Everything it decides is a `GameLoop` call, and
  `GameLoop` is fully tested; what remains is async choreography over DOTween and UniTask,
  verified in Play for every change (the record is `.claude/notes/case-status-log.md`).
  Splitting it into testable presenters was considered and deferred: the case ranks
  bug-free above architecture, and a rewrite of the orchestrator days before delivery is
  the wrong trade.
- **Static `GameRules` and no interfaces on the domain types.** Nothing ever substitutes
  them; an interface would be a seam nothing swaps.
- **Every tuning number is a serialized struct on the component that uses it**, with a
  `Defaults` initializer, so a retune never recompiles and a new field arrives with a
  value instead of a zero.

## Repository map

```
Assets/00_GAME/Scripts/<Layer>/    one asmdef per layer, tests in Scripts/Tests/EditMode
Assets/00_GAME/Levels/             the JSON levels
Assets/00_GAME/Scenes/Game_Scene   the one scene
level-editor/                      the HTML level editor and its node tests
Docs/PLAN.md, Docs/PERFORMANCE.md  the design plan and the performance ledger
```
