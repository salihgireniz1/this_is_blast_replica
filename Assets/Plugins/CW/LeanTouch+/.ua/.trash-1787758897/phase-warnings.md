# Phase warnings — LeanTouch+ analysis

## Phase 0
- Directory is NOT a git repository. No commit hash available; `meta.json` gitCommitHash will be empty.
  Consequence: no incremental updates possible on future runs — every `/understand` here is a full rebuild.
- corepack (v0.29.4) is broken on this machine ("Cannot find matching keyid"). Plugin core was built via
  `npx --yes pnpm@10` instead. Not project-related.

## Phase 0.5
- 417 of 480 files excluded via `.understandignore`: 247 `*.meta` GUID sidecars, 160 `Examples/**/*.unity`
  demo scenes, plus binary media (png/prefab/mat/DAE/asset). User confirmed this scope.
- First scanner pass picked up the tool's own `.ua/config.json` and `.ua/.understandignore` as project
  files (60 instead of 58); re-run with `--exclude ".ua/*,.ua/**"`. Upstream scanner should self-exclude
  its data dir.

## Phase 1
- **Import map is completely empty** (58 entries, 0 edges) — verified, not a bug. C# `using` resolves to
  namespaces, not file paths; all 54 real scripts sit flat in `Required/Scripts/` sharing namespace
  `Lean.Touch`. No `imports` edges exist anywhere in this graph by design.

## Phase 1.5
- Semantic batching had no import graph to cluster on, so it fell back to consolidating 58 singletons into
  3 alphabetical batches. `neighborMap` was empty for all 3 batches.
- Compensated by injecting a deterministic grep-derived type inventory (133 declarations, with internal vs
  external base-class split) into every analyzer prompt, so cross-batch `inherits`/`implements` edges were
  still emittable.

## Phase 2
- **Systematic tree-sitter gap (all 3 batches):** the C# extractor returned only the first/runtime class
  per file. It missed every `*_Editor` class (they live inside `#if UNITY_EDITOR` in a second namespace,
  `Lean.Touch.Editor`), every nested `FingerData : LeanFingerData`, all enums, and the `Line` struct in
  LeanShapeDetector. All 3 analyzers supplemented from grep. Editor-class `lineRange.end` values are
  therefore approximations (starts are exact).
- The bundled extractor also skipped `LeanTouchPlus.asmdef` and `Required/Documentation.html` entirely.
- Nested `FingerData` classes and enums deliberately NOT emitted as nodes (small private state helpers);
  described in parent class summaries instead.
- No edges to external base types (`MonoBehaviour`, `CwEditor`, `LeanDragTrail`, `LeanFingerDown`,
  `LeanFingerTap`, `LeanSwipeBase`, `LeanSelectableBehaviour`, `LeanSelectableByFingerBehaviour`).
  Consequence: `LeanDragLine`, `LeanFingerDownCanvas`, `LeanFingerTapExpired`, `LeanFingerFlick`,
  `LeanManualFlick`, `LeanManualSwipe` and the `LeanSelectable*` family have NO `inherits` edge — their
  base classes live in the sibling LeanTouch/LeanCommon packages, outside this analysis scope.

## Source-code findings (real defects in the asset, not analysis artifacts)
- `Required/Scripts/LeanSelectSelf.cs` is a 26-byte stub: UTF-8 BOM + `// Remove`, no code.
- Three editor classes are copy-paste leftovers whose names don't match their file:
  - `LeanMultiTwist.cs`  declares `LeanDestroy_Editor`
  - `LeanPickable.cs`    declares `LeanSelectSelf_Editor`  (orphaned remnant of the deleted LeanSelectSelf)
  - `LeanSelectableTime.cs` declares `LeanPlane_Editor`
  These were preserved under their real names, not silently normalized.
