# This Is Blast — replica

A from-scratch rebuild of Voodoo's *This is Blast!*, built as a **portfolio case study**.
It will never ship. The deliverable is the engineering: architecture, tests, profiler
evidence, and the written reasoning behind every decision — including what was
deliberately not built.

Full plan: **`Docs/PLAN.md`** — mechanics analysis, art bible, animation bible,
architecture, performance budget, test strategy, build phases, campaign design.
Read the relevant section before starting a phase.

Reference prototype, for **art and mechanics only — never copy its code**:
`C:\Users\giren\Desktop\Projects\this-is-blast-clone`.

House rules live in `.claude/rules/` (auto-loaded every session): how we work chunk by
chunk, the code standard, how the Unity Editor is driven, and **`case-brief.md` — the
Apps case requirements, which override every other file in this repo unless Salih says
otherwise in chat.**

## Where the record lives

This file is the map. The detail is in `.claude/notes/` — **not auto-loaded, read on
demand** with `Read`/`grep` when a task touches that area:

| File | Read it when |
|---|---|
| `.claude/notes/case-status-log.md` | Touching any built component. Every chunk of the case in build order: test count, what Play verified, what was rejected and why, every trap hit. **The record; when a summary disagrees with an entry there, the entry wins.** Grep by component name or `trap`. |
| `.claude/notes/editor-version-incident.md` | Editor boots in Safe Mode, `git status` shows `ProjectVersion.txt`, or opening from the Hub. The 6000.5 -> 6000.0.68f1 wreckage and the recovery recipe. |
| `.claude/notes/colour-space-and-original-game.md` | Materials, tints, TCP2 ramp values, or reading numbers from the shipped APK (UnityPy recipe, measured values). |
| `.claude/notes/portfolio-phases.md` | After the case ships. Phases 0-1 of the portfolio plan, naming decisions, deferred triggers. |
| `Docs/PERFORMANCE.md` | Any performance work. The ledger: targets, device, every step's before/after, what was measured and not taken. |

Auto-memory (`~/.claude/projects/.../memory/`) holds user preferences and the case spec
digest; it is loaded separately.

## Architecture

Dependency direction is one-way, enforced by asmdef references rather than by convention:

```
Blast.Domain          pure C#, noEngineReferences: true
  ↑
Blast.Application     use cases, UniTask
  ↑            ↑              ↑
Presentation   UI (MVVM, R3)  Infrastructure
  ↑
Blast.Bootstrap       VContainer composition root
```

`ArchitectureTests` (EditMode) verifies this from the asmdef files on disk. Adding a
layer means adding it to that test's table first, or the test fails.

Scripts: `Assets/00_GAME/Scripts/<Layer>/`, tests in `Scripts/Tests/EditMode`
(assembly `Blast.Tests`). Levels: `Assets/00_GAME/Levels/Level_01..06.json`.
Level editor (HTML, no deps, double-click `index.html`): `level-editor/`, tests
`node --test level-editor/logic.test.js`. Level generators: `Docs/Tools/generate_level_0X.py`.

Unity **6000.0.68f1** — fixed by the case brief. The project was downgraded to it from
6000.5.7f1 and was silently re-upgraded once by the Hub (its own database, not
`ProjectVersion.txt`, decides the editor; `-automated` auto-answers the mismatch dialog).
Both cleared; recipe in `editor-version-incident.md`. **Standing check:**
`ProjectVersion.txt` is in git, so a silent upgrade is the first line of `git status` —
look there before anything else when the Editor behaves strangely after an open. **If a
package pin ever disagrees with `packages-lock.json`, the manifest is the thing that is wrong.**

Stack: URP **17.0.4** linear, VContainer, R3, UniTask, DOTween, LeanTouch+, Addressables,
Toony Colors Pro 2 (Apps' self-contained `CustomShader` for the game art; the `JMO Assets`
store install is kept for the generator only).

## Performance targets

120 FPS / 8.33 ms, **0 B GC alloc per frame** during gameplay, worst case 360 cubes.
Measured on a Samsung Galaxy A16 over adb; editor numbers are not usable for render or
GC counts. Shipped state (`Docs/PERFORMANCE.md`): `Level_01` locked at the phone's
refresh rate, 0 B/frame idle, ~11 B/frame of game allocation during a burst.

## Status

**THE CASE IS LIVE — read this before anything below.** The interview case from Apps
(apps.com.tr) arrived 2026-09-01 and is built **in this project**. Deadline: **Monday
2026-09-07**, delivered by mail after the final push. The repo is
`github.com/salihgireniz1/this_is_blast_replica` (private, `info@apps.com.tr` invited).
Brief: `Game Developer Case.pptx` in Downloads; rules in `.claude/rules/case-brief.md`.
The short version: This is Blast core WITHOUT merge, 10x10 cube grid, 5 slots, top
shooter row selectable + 2 queue rows visible, Hidden Shooter feature, JSON-driven sample
level (solvable, all 5 colours, includes a hidden), WIN/LOST overlays with a restart
button that replays the same level, direct play on Editor Play, Unity 6000.0.68f1 + URP.
Evaluation order: bug-free > juiciness > architecture > performance > git usage.

**Where it stands (2026-09-04): the case is feature-complete. Unity suite 84/84, level
editor 41/41 node tests.** What exists, by layer (details per component in the log):

- **Domain** — `Cell`, `BlastColor` (byte, values pinned), `BoardModel` (nothing moves:
  authored array + per-column front index, top layer dies first), `Shooter` +
  `ShooterQueue` (only `TakeFront`; hidden = concealed at depth > 0), `SlotRow` (occupancy
  IS the ammo), `GameRules` (static readers: `TryFindTarget`, `IsWon`, `IsFailed`),
  `ColumnState` (Locked -> Settling -> Free, counts per column).
- **Application** — `GameLoop`: `TrySelect` / `TryShoot` (guards are input rules, never
  exceptions; asks IsWon before IsFailed), `LockColumn` / `MarkSettling` / `MarkSettled`,
  `event Decided`. No time in this layer; Presentation paces it.
- **Infrastructure** — `LevelDefinition` (JsonUtility DTO, lowercase fields = the file
  contract: `boardLayers[].rows`, `slotCount`, `shooterColumns[].shooters[]`), `LevelParser`
  (the trust boundary: every refusal is a FormatException naming the location), `PaletteData`.
- **Presentation** — `LevelSpawner` (view registry + every layout number as structs),
  `GameDirector` (LeanTouch tap -> select -> UniTask fire loops; settings structs
  `ShooterMotion` / `Firing` / `CubeDeath`; every await cancels on destroy), `CubeView`,
  `ShooterView` (animator, yaw tween, outline as second material, counter punch),
  `ComponentPool<T>` + `ShotPools`, `ShotAudio` (two voices round-robin), `CameraShake`,
  `CollapseTweens`, `PerfProbe` / `PerfSweep` (dev-only).
- **UI** — `LevelEndViewModel` (R3, `IsShown` / `Title` WIN|LOST / `Restart` command),
  `LevelEndView` (humble, fade + title pop).
- **Bootstrap** — `GameLifetimeScope`: parses `_level` (**boots `Level_01`**, the case's
  level) and registers everything in VContainer: the models as instances, `GameLoop` /
  `LevelEndViewModel` / `IColorMaterials -> PaletteColorMaterials` as singletons, the
  scene components via `RegisterComponent` (their `[Inject] Construct` runs at build), and
  `SceneRestarter` (entry point: `Restart` = active scene reload). No save system — deleted with
  Easy Save 3 on 2026-09-04, do not reintroduce it for the case.

**Facts that must hold when the case ships:** the scope's `_level` is `Level_01`; the
`Dock`'s five slot markers are Salih's hand-tuned numbers, do not touch; `Level_01.json`
is not re-saved from the level editor (it would re-flow); `Level_02`/`03`/`04` stay as
the stress levels behind the performance ledger. House rule from Salih: chain tweens with
`.ToUniTask()` + `await` or `.Forget()`, never `Sequence`/`Append`.

The portfolio phase plan (`portfolio-phases.md`) resumes after the case ships; the case
overrides it wherever they disagree (no merge feature, their art, 10x10 single layer).

## Running the tests

```
unity cmd run_tests --mode editor --filter "Blast.Tests" --filter_type assembly --async_tests true
```

then poll `test_status` (also in `Temp/pipeline_test_status.json`). **Filter by
assembly**: unfiltered, the run pulls every package's EditMode suite and times out at six
minutes; filtered it is under 2 s. `recompile` first; `run_tests` against stale
assemblies reports the old code.

## Traps that cost hours (the short list — the log has the full story of each)

- **Editing an asset on disk does not reimport it**, and this session's Unity does not
  auto-refresh even when focused (most likely the `-automated` flag). After changing a
  level file: `AssetDatabase.ImportAsset(path, ForceUpdate)` via eval, or Ctrl+R in Unity.
- **A modal dialog freezes every pipeline command** (5-30 s timeouts on a trivial eval
  while the process is alive): URP global-settings confirmation, the test runner's "Scene(s)
  Have Been Modified", package menus. Ask Salih to look at the window; do not restart.
  Write scene values and start tests in separate steps.
- **The EditMode runner hangs at "running"** while the editor is in Play mode
  (`cancel_tests` + `editor_stop`), and sometimes after a `recompile`; the unwedge eval in
  `unity-mcp.md` fixes a stale assembly, and a Player build has cleared the hang every time.
- **A new field inside a serialized struct arrives as 0** unless the struct field has an
  initializer (`= Defaults`); **moving a field into a struct changes its serialized path**.
  Write scene values through `SerializedObject` and check the declaration first.
- **A compile triggered while Play is running** (an eval with a syntax error will do it)
  can drop `GameDirector`'s serialized references and leave the scene dirty. It reads as
  "shooters fire, no cube dies". Do not save; reopen the scene from disk; check `isDirty`.
- **Console reads:** entries carry `level` and `timestampUtc` (not `type`), and old runs
  are returned too. Filter on both against a stamp taken before the probe.
- **Play-mode probes:** set `Application.runInBackground = true`, log from an
  `EditorApplication.update` hook (not polling evals), and unsubscribe it on
  `playModeStateChanged` — a leaked logger throws every editor frame until a reload. A
  decision forced from inside an eval lands on a stretched frame (0.333 s clamp) and
  tweens finish before the first sample. An allocation measurement must prove the work
  happened (cube count in the record).
- **`eval` drops `using` directives** — fully qualify every type. An eval that reports the
  5 s main-thread timeout may still have run; check the console before installing a copy.
- **Prefab apply with a deactivated scene instance** applies `active=false` to the prefab
  root. Re-apply with the instance active, then deactivate without applying.
- **`capture_game_view` renders the camera only**; a Screen Space Overlay canvas is
  invisible in it. `unity cmd screenshot` shows the overlay.
- **When Salih reports a look regression or a misplaced visual:** bisect with captures at
  the pre-change commit, and read the reported Transform values to find which object the
  coordinate belongs to, before touching anything.
- **When a juice note says "take the clone's values", take the ORDER of the tweens too.**
  DOTween punch `vibrato` is per SECOND (`(int)(vibrato * duration)` segments; under 3
  there is no bounce). A reused `SetAutoKill(false)` tween never kills, so await it with
  `AwaitForComplete`, not `ToUniTask()`; `Tweener.ChangeEndValue(object)` boxes, use the
  typed `TweenerCore` overload.

## Recording progress

When a chunk turns green, add its entry to the **end** of
`.claude/notes/case-status-log.md` in the same turn (done marker, test count, what Play
verified, what was rejected, any trap), and update the "Where it stands" paragraph above
only when the summary itself changes (test count, a new component, a shipped-state fact).
That log is the only place a new session learns where the work stands.
