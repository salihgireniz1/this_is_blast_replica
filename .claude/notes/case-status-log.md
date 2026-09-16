# Case status log, chunk by chunk

Moved out of `CLAUDE.md` on 2026-09-04. Every chunk built for the Apps case, in the order it was built, with the test count, what was verified in Play, what was rejected and why, and every editor trap hit on the way. This is the record; when a summary elsewhere disagrees with an entry here, the entry wins. Search it by component name (`GameDirector`, `LevelSpawner`, `ColumnState`, ...) or by the word `trap`.

**Case status:**
- Art swap — done, 22/22 green. `AppsAssets.unitypackage` imported (their WalkingCube
  shooter, Gun, Cube.fbx, per-color materials + Hidden + Outline, self-contained TCP2
  `CustomShader`, SplashEffect, Splash.wav, Baloo2 font). `Palette.asset` rows now bind
  the AppsAssets materials (`Surprise` → `Cube_Hidden`), tints synced from each
  material's `_BaseColor`. Deleted: `00_GAME/Materials`, `00_GAME/Meshes` (RoundedCube),
  `JMO Assets` (49MB paid TCP2 install — their materials bind the generated shader inside
  AppsAssets, nothing needed the store install). **Restored 2026-09-01** at Salih's call,
  for the juice pass (outline, rim, the shader generator itself): `git checkout 7b91b13~1
  -- "Assets/JMO Assets"`, 740 files, imports with zero errors and no shader-name collision
  with `CustomShader`. Keeping it is a presentation cost, not a technical one. Scene keeps the camera/light/volume rig,
  the scope, imported `GameArea`+`Floor`, and a `Cannon` object Salih is hand-building
  into the shooter prefab (WalkingCube + ammo TMP text).
- `Shooter` + `ShooterQueue` (Domain) — done, 28/28 green. `Shooter` is a readonly struct
  of authored facts only (colour, ammo, hidden flag); concealment is answered by the queue
  (`IsRevealed`: visible anywhere, hidden only at depth 0) because it is a question about
  position, not about the shooter. Same nothing-moves shape as `BoardModel`: jagged
  authored arrays plus a per-column front index. Only `TakeFront` exists, so "only the
  front row is selectable" is enforced by the API shape rather than checked at runtime.
  Probe: dropping the `depth == 0` term turned exactly one test red with the right message.
  The depth check exists because a negative depth resolves to an already-taken shooter —
  a valid array slot, so nothing would ever throw without it.
- `SlotRow` (Domain) — done, 33/33 green. Occupancy IS the ammo: a slot with shots left
  is occupied, zero is empty — one array instead of a parallel bool[] that could disagree.
  `Occupy` seats in the first empty slot (a freed middle slot included), `Spend` frees at
  the last shot, so a dead shooter can never hold a slot; "ammo left but no target" is
  simply a slot nobody calls Spend on, which is the pressure `IsFull` reads for the fail
  condition. Slot count is a ctor param (the case's five is the level file's fact, not a
  magic number here). Probe: reversing the Occupy scan turned the order test red with the
  right message ("The first shooter skipped slot 0"), plus two collateral reds.
- `BoardModel.TryFrontColor` + `GameRules` (Domain) — done, 42/42 green. `TryFrontColor`
  answers "what colour does Remove take next" (top LIVING layer of the front row, false
  for a spent column). `GameRules` is a static class of pure readers — no state, nothing
  to substitute, so no interface: `TryFindTarget` (leftmost matching front; only fronts
  are shootable, spent columns skipped), `IsWon` (every column spent), `IsFailed` (every
  slot occupied AND no occupant has a target — a free slot or one working shooter means
  not stuck). The caller asks IsWon first: an emptied board with a full row satisfies
  IsFailed's letter too. Probes: reading the top authored layer instead of the living one
  turned exactly the aimed test red; dropping the IsFull half was caught by SlotRow's own
  empty-slot guard (and the assert would catch the skip-empties variant).
- `LevelDefinition` + `ParsedLevel` + `LevelParser` (Infrastructure) — done, 49/49 green.
  The JSON schema: `boardLayers` (ground layer first, each `{"rows":[...]}`; rows are
  letter strings Y/R/B/G/O, index 0 = the FRONT row, one letter per column; `boardRows`
  was the single-layer shape until 2026-09-02 — dimensions derive from string length and row count, so nothing can
  disagree), `slotCount`, `shooterColumns` (each `{"shooters":[...]}` front-first with
  `color`/`ammo`/`hidden` — the wrapper object exists because JsonUtility cannot read
  jagged arrays). The DTO's fields are lowercase on purpose (JsonUtility maps strictly by
  name; the type IS the file contract, the future HTML editor writes it too). The parser
  is the trust boundary: JsonUtility never fails on missing keys — it hands back nulls
  and zeros — so every refusal is an explicit FormatException naming the file location;
  nothing is defaulted. Zero ammo is refused HERE because SlotRow reads zero as an empty
  slot. Probe: building the grid upside down turned exactly the aimed test red.
  Level files live in `Assets/00_GAME/Levels/` — the future HTML editor's export target.
- `Level_01.json` + `LevelFileTests` — done, 50/50 green. The sample level: 10x10 board in
  horizontal colour bands, exactly 20 cubes AND 20 ammo per colour (every shooter drains
  fully and leaves — no shooter can end up stranded with leftover ammo); 5 queue columns
  x 3 deep, the five initial fronts are the five big shooters (R10/B10/Y10/G10/O10 — seat
  all five and the first five bands clear themselves); two hidden shooters at depths 1-2
  that reveal mid-game. Full playthrough verified on paper: at every band the needed
  colours are at reachable fronts. `LevelFileTests` walks every file in
  `Assets/00_GAME/Levels/` and asserts the case brief per file: parses, all five colours,
  a hidden shooter, and ammo >= cubes per colour (under-ammo = unwinnable, the mistake
  that only shows at the very end of a playthrough; over-ammo stays a legal choice).
  **Editor trap, hit during the probe: editing an asset on disk does NOT reimport it** —
  run_tests read the stale TextAsset and stayed green until an explicit
  `AssetDatabase.ImportAsset(..., ForceUpdate)`; `recompile` does not cover it.
- First visible build — done, 50/50 green. Play now shows the full level: 100 cubes in
  the authored bands on GameArea, 15 shooters in 5 queue columns with ammo counters, the
  two hidden ones wearing `Cube_Hidden` and a "?" label. The pieces:
  `IColorMaterials` (Presentation defines the seam — it may not see Infrastructure — and
  Bootstrap's `PaletteColorMaterials` adapts `PaletteData` onto it; `HiddenMaterial` is
  the palette's Surprise row), `CubeView` + `ShooterView` (humble: sharedMaterial only,
  they render state and never compute it), `LevelSpawner` (serialized layout: board
  origin (-4.275, 0.45, -4.275), cell 0.95 — GameArea's inner floor is 9.5 x 9.5 so ten
  cells span it exactly; queue origin z=-7.5, spacing 1.5/1.2, columns centred on x=0).
  `GameLifetimeScope` parses the level once (only Bootstrap sees both parser and views),
  registers the three domain models, and hands the spawner its dependencies through
  `Construct`. Prefabs: `CubeView.prefab` (their Cube.fbx at 0.9 scale),
  `ShooterView.prefab` (Salih's Cannon: WalkingCube rig + Count_Text TMP; coloured parts
  are the CubesWalk MeshRenderer and the CashierBody SkinnedMeshRenderer). The scene
  keeps both template instances deactivated - the Cannon one is the prefab's source.
  `Blast.Presentation.asmdef` gained `Unity.TextMeshPro` (third-party refs are outside
  the architecture test's scope, verified before adding).
- `GameLoop` (Application) — done, 57/57 green. The sync use-case core: `TrySelect`
  (guards double as input rules — decided game / spent column / full row are "nothing
  happens", never an exception; seats atomically; re-checks fail), `TryShoot` (one ammo
  for one cube in the same call, reports the hit column; asks IsWon BEFORE IsFailed
  because an emptied board with a stuck-full slot row satisfies the fail's letter too —
  there is a test pinning exactly that). No time in this layer: Presentation paces the
  calls, which is why the whole use case tests without mocks. Probe: swapping the verdict
  order turned exactly the ordering test red.
- `GameDirector` + `LevelSpawner` registry (Presentation) — done, verified live in Play:
  two columns selected, both shooters ran to slots, drained 10 kills each at the fire
  rhythm, the columns flowed forward, the queue stepped up and revealed, both drained
  shooters ran off-screen. The spawner became the view registry (mirrors the domain's
  nothing-moves bookkeeping: views stay in lists, a front index walks), owns all layout
  numbers including slot positions (z=-5.9, spacing 1.5). The director is the async glue:
  Input System tap -> raycast -> `TryGetSelectableColumn` (only a column's front view
  accepts), then UniTask fire loops. **Each fire loop counts its OWN ammo instead of
  watching the slot** — the slot frees on the last shot and the player may seat a new
  shooter into it before the next tick; a loop keyed on occupancy would fire the new
  tenant's ammo with the old tenant's view. ShooterView prefab gained a BoxCollider.
  Verdict is announced via Debug.Log until the UI chunk. GameArea's planes also got their
  actual materials (they imported with default Lit).
  Presentation asmdef gained UniTask, UniTask.DOTween (awaiting a tween needs the
  extension assembly, not just the define) and Unity.InputSystem; the project is
  Input System-only (`activeInputHandler: 1`), so input reads `Mouse.current`.
- LeanTouch input + bullets — done, 57/57 green. Input is `LeanTouch.OnFingerTap` ->
  `finger.GetRay(camera)` (Salih's call: LeanTouch over hand-rolled Input System reads;
  the asmdef swapped Unity.InputSystem for LeanTouch). The scene gained the LeanTouch
  runner and an EventSystem + InputSystemUIInputModule — LeanTouch's IsOverGui logs an
  error without one. Every shot spawns a small cube bullet (CubeView prefab at 0.3 scale)
  wearing the shooter's material; the cube dies on impact, the column flows after. The
  cube VIEW is still popped at fire time, not impact time — registry order must match
  domain removal order or two in-flight shots at one column swap victims.
  **Prefab trap, hit and fixed:** applying prefab overrides while the scene's template
  instance was deactivated applied `active=false` to the prefab root too — every clone
  spawned disabled. Apply runs on ALL overrides, so re-apply with the instance active,
  then deactivate WITHOUT applying; the template's off-state stays an instance-only
  override now.
- Tap-to-select rewired onto LeanTouch's own chain — done, 57/57 green, verified in Play
  (a `Select` through `LeanSelectByFinger` popped the front shooter, zero console errors).
  Salih's call: use the "15 Tap To Select" example's layout instead of a hand-written
  `OnFingerTap` subscription and raycast. Scene object `Tap To Select` = `LeanFingerTap`
  (`IgnoreStartedOverGui` + `IgnoreIsOverGui` on, replacing the old `IsOverGui` check)
  -> `LeanSelectByFinger.SelectScreenPosition` -> `GameDirector.OnShooterSelected`, both
  hops as inspector-wired persistent listeners; the director no longer owns a camera,
  an enable/disable pair or a raycast. `ShooterView.prefab` root carries a
  `LeanSelectableByFinger`; the query is Raycast on default layers, Search
  `GetComponentInParent` (the collider sits on a child). Selection state means nothing to
  the game, so it is kept at exactly one entry: `Limit` DeselectFirst, `MaxSelectables` 1,
  `Reselect` DeselectAndSelect, `DeselectWithNothing` off — every tap raises `OnSelected`
  and the list never grows. Presentation asmdef gained `LeanCommon` (`LeanSelectable`
  lives there, not in `LeanTouch`). No dedicated layer: cubes and bullets have no
  colliders, so the raycast can only ever hit shooters. **Trap:** the editor had the
  LeanTouch example scene open when the wiring script first ran, so `GameObject.Find`
  touched the example's object in memory; `EditorSceneManager.OpenScene` to `Game_Scene`
  first, and `git status Assets/Plugins` afterwards to prove the example was not saved.
  The `eval` tool also drops `using` directives — fully qualify every type.
- Bullet prefab + muzzle splash — done, 57/57 green, verified in Play (five fronts seated,
  cubes 101 -> 66, splashes spawned per shot and all gone eight seconds later, zero errors).
  Salih authored `00_GAME/Prefabs/Bullet.prefab` (Cube.fbx variant, `Gun.mat` body,
  `Bullet_Trail` TrailRenderer, rotated -90 on X); it now carries a `CubeView` whose
  renderer is the body, so the director's `_bulletPrefab` field kept its type and the
  per-shot `Wear(shooter material)` kept working. `_bulletScale` is gone: the prefab IS the
  size, and the bullet spawns with the prefab's own rotation, not identity. The splash is
  `AppsAssets/Prefabs/SplashEffect.prefab` instantiated at the muzzle every shot; its root
  ParticleSystem's Stop Action was None (it would have leaked one object per shot) and is
  now Destroy, so the director never times it. `CubeView`'s header now says it dresses a
  board cube OR a bullet - one humble type, not a `BulletView` twin. Still Instantiate +
  Destroy per shot, marked `ponytail:` for the measured pass. The splash prefab's
  AudioSource has Play On Awake but no clip; `Splash.wav` goes in with the sound chunk.
- Shooter animator wired — done, 57/57 green, verified in Play (isRun flips the frame a
  front is selected, seated shooters sample as Shoot while firing and Idle while waiting,
  queue shooters stay Idle, zero errors). Salih added `isIdle` / `isRun` booleans and a
  `Shoot` trigger to `AppsAssets/Animations/WalkingCube.controller`; the three Any State
  transitions had **no conditions**, which makes every one of them true every frame, so the
  animator was restarting itself continuously. Now: Any->Run on `isRun`, Any->Idle on
  `isIdle` (both with Can Transition To Self OFF, or a held bool would restart the clip
  each frame), Any->Shoot on the trigger with self-transition ON (each shot restarts the
  clip) and its blend cut from 0.25 to 0.05 s - the shoot clip is 0.32 s against a 0.22 s
  fire interval, so a 0.25 s blend would never reach the pose. `ShooterView` gained an
  `Animator` reference (the WalkingCube child) and two methods: `SetRunning(bool)` writes
  both booleans as each other's opposite, `PlayShoot` drops `isIdle` before pulling the
  trigger so the Idle transition cannot cut the shot short. The director calls
  `SetRunning(true)` before the run to the slot and before the leave, `SetRunning(false)`
  on arrival and on every targetless tick, `PlayShoot` on every successful `TryShoot`.
  Parameter names are `Animator.StringToHash` statics, no per-call string hashing. Layout
  note: Salih moved the queue to origin z=-13 with 2/2 spacing and the slots to z=-9; the
  older numbers quoted above are superseded by the scene.
- `SplashPool` (Presentation) — done, 58/58 green, verified in Play (40 prewarmed, five
  slots chain-firing peaked at 20 active, the instance count never grew past the prewarm
  once it covered the peak; before the bump it grew 16 -> 27 and then held). Grow-only
  pool: a list of every instance, `PlayAt` re-lights the first switched-off one or clones
  another, `Prewarm(n)` fills it before the first shot. **No return callback and no
  PooledX component**: the prefab's Stop Action is Disable (was Destroy), so Unity switches
  a finished splash off itself and "off" is the only signal the pool reads; switching it
  back on replays Play On Awake on the root and the InnerSplash child. Finding a free one
  is a linear scan over a few dozen entries, marked `ponytail:`; `UnityEngine.Pool.
  ObjectPool` was the alternative and lost on needing a callback component to release.
  Prewarm is 40 = five slots x (1.7 s splash life / 0.22 s fire interval). The test
  (`SplashPoolTests`, the first Presentation test; the test asmdef now references
  `Blast.Presentation`) simulates a finished splash by deactivating it and asserts a third
  instance is NOT created — red first with "expected 2 but was 3". The bullet is still
  Instantiate/Destroy per shot, still marked for the measured pass.
- Shooters face where they go and what they shoot — done, 58/58 green, verified in Play
  (seated shooters sampled at yaw -3..-46 while firing at cubes across the board, back to
  0 the tick they ran out of targets). The clone has the same mechanic (turn toward the
  move direction, snap toward the target on each shot, drift back to forward); ours is two
  `ShooterView` methods, `TurnTo(worldPoint, duration)` = `DOLookAt` constrained to yaw,
  and `FaceForward(duration)` = rotate to identity. Both kill the previous turn tween
  first, or a run-turn and an aim-turn on the same transform would fight. The director
  calls TurnTo(slot) before the run and TurnTo(offScreen) before the leave, FaceForward on
  arrival and on every targetless tick, TurnTo(cube) at every shot. One `_turnDuration`
  (0.15 s) serves all three; split it if the aim ever wants to be snappier than the run.
  The ammo counter is a child, so it yaws with the body - the original's does too.
- `ComponentPool<T>` (Presentation) � done, 61/61 green. `SplashPool` generalised at Salih's
  call so a bullet pool would not be a second, differently shaped pool: one grow-only class
  for any `Component` prefab, `Take(position)` (places first, then switches on - Play On
  Awake must fire at the new spot), `Return(item)` (switches off), `Prewarm(n)`. "Off" is
  still the only free-signal; a splash goes off by its Stop Action, a bullet by the
  director's Return after impact. `SplashPool.cs` and its test are gone (both were
  uncommitted); `ComponentPoolTests` has three tests: Unity-switched-off reused, Returned
  reused, Take places. Probe: emptying `Return` turned exactly the Returned test red
  ("A returned instance was not reused; a third was instantiated").
- Bullet pool + pool parents in `GameDirector` � done, 61/61 green, verified in Play (a full
  level: 100 shots through exactly 5 bullet instances, `BulletPool` never grew past its
  prewarm, `SplashPool` held at 40, verdict `Won`, zero gameplay errors). `Construct` makes
  two empty children, `SplashPool` and `BulletPool`, and each `ComponentPool` spawns under
  its own; the director's transform itself holds nothing. The bullet is `Take(muzzle)` +
  `Wear`, then `Return` after the flight tween - the Instantiate/Destroy per shot and its
  `ponytail:` marker are gone. `_bulletPrewarm` is 5: one per slot, because a flight
  (0.12 s) ends before the next shot (0.22 s). No director test: the reuse rule lives in
  `ComponentPoolTests`, the director only calls it. Unverified by eye, the CLI round trip
  is slower than a flight: the bullet's TrailRenderer on reuse. `Take` moves the bullet
  while it is off and switches it on afterwards, so no streak is expected; watch the first
  reused shot in Play once.
- `ShotPools` (Presentation) - done, 61/61 green, verified in Play (`Pools/BulletPool` held 5
  and `Pools/SplashPool` 40 children on entering Play, zero errors). Salih's call: the director
  carried six pooling fields plus a `NewChild` helper that were a second responsibility, so
  they moved to a MonoBehaviour on a scene object `Pools`: two `PoolSettings<T>` rows (prefab +
  prewarm, a serializable generic struct - Unity serializes concrete generic fields since
  2020.1) built and prewarmed in `Awake`, exposed as `Bullets` / `Splashes`. The director
  holds one inspector reference (`_pools`) and only calls `Take` / `Return`. Awake order
  against the scope is irrelevant: nothing reads a pool before the first tap. No new test -
  no new branch; the reuse rule stays in `ComponentPoolTests`. Scene re-wired via eval with
  `SerializedObject` paths (`_bullets.Prefab` ...); the director's tuned `_fireInterval` 0.16
  survived because field names outside the moved six did not change.
- Director settings as structs - done, 61/61 green, verified in Play (live read
  `_firing.Interval` 0.16, `_motion.RunDuration` 0.45, zero gameplay errors). The ten loose
  floats became three `[Serializable]` nested structs, one inspector heading each:
  `ShooterMotion` (run / turn / step / leave duration / leave distance), `Firing` (interval /
  muzzle height / flight duration), `CubeDeath` (shrink / flow). Public PascalCase fields
  (a settings bag, no invariants to guard), a static `Defaults` per struct so a fresh
  director carries the same numbers the loose fields did. **Moving a field into a struct
  changes its serialized path**, so the scene's values were re-written through
  `SerializedObject` (`_firing.Interval` etc.) and the tuned 0.16 carried across by hand;
  `FormerlySerializedAs` cannot cross into a nested struct.
- Cube death and flow re-timed to the clone's numbers - done, 61/61 green, verified in Play
  (two columns drained 100 -> 80 cubes, every tween settled, zero errors). Salih's report: cubes
  vanished instantly and the survivors' slide had no weight. Two causes. The shrink was 0.12 s
  `InBack`, which spends its first half growing and then collapses in ~0.05 s - it reads as a
  pop, not a shrink; now 0.15 s `OutQuad` to zero, exactly `ColorCube.DestroyCube`. And the flow
  started at impact, concurrent with the shrink, so the two beats blurred into one; now
  `ShotVisual` awaits the shrink, destroys, THEN calls `FlowBoardColumn` - the clone's
  `SetDelay(_hideDuration)` expressed as sequencing. The slide is `OutSine` and lands with the
  clone's wobble: `DOPunchPosition(back * 0.1, 0.15 s, vibrato 2, elasticity 0.5, OutBounce)`
  from an `OnComplete`. `CubeDeath` gained `SettleDistance` / `SettleDuration` (written into the
  scene via eval - a new serialized field on an existing struct arrives as 0, not as the C#
  default). The kill-then-absolute-target rule already heals a bounce cut short by the next
  flow. The `OnComplete` closure allocates per cube per shot, marked `ponytail:` for the
  measured pass. **Rule this confirms:** when a juice note says "take the clone's values", take
  the ORDER of the tweens too, not only the durations.
  **Then swapped back to `InBack` at Salih's call** - he wants the swell-then-collapse he
  remembers from the original, which the clone does not do. Why InBack looked like an instant
  pop before: DOTween's default overshoot (1.70158) peaks at only +10% scale at 42% of the
  tween, three frames at 0.12 s. Now `SetEase(Ease.InBack, ShrinkOvershoot)` with a new
  `CubeDeath.ShrinkOvershoot` field at 3 (+25% at mid-tween) over 0.15 s. If it still reads
  weak, the agreed fallback is a two-step Sequence (grow 0.06 s OutQuad to 1.25, collapse
  0.12 s InQuad) so swell and collapse tune independently.
- Held columns (`GameLoop.HoldColumn` / `ReleaseColumn`) - done, 63/63 green, verified in Play
  (five fronts seated, 100 -> 50 cubes, every column released at the end, zero errors). Salih's
  report: shooters fired at cubes that were still sliding forward, because the domain removes
  the cube at fire time and the next cube is the front in the SAME call - while the bullet has
  0.12 s of flight, the death 0.15 s and the flow 0.15 s still to show. The fix lives in
  Application, the layer that mediates the time-free domain and the paced presentation: a
  `bool[] _held` per board column, `TryShoot` passes it to a new `GameRules.TryFindTarget`
  overload that skips held columns (the leftmost UNHELD match, so a shooter hops columns while
  one settles instead of stalling). **`IsFailed` deliberately does NOT see the mask**: a held
  column's cubes are on their way, so a full row waiting on it is paused, not stuck - the second
  test pins that, and it is the one that would go red if someone "helpfully" threads `_held`
  into the verdict. Director: `HoldColumn` synchronously after the pop (before the first await,
  so no other shooter's TryShoot in that frame can pick the column), `ReleaseColumn` after
  awaiting the slide `FlowBoardColumn` now returns (`Tween`, null when the column is spent; the
  UniTask awaiter completes on kill too). Consequence: a single-colour column now drains at
  ~0.45 s per shot instead of 0.16 s - flight + death + flow - which is the pacing Salih asked
  for; the fire interval only governs the hop between free columns. Red first with
  "The shot went into the column that is still settling. Expected: 1 But was: 0".
- Drained shooters leave sideways - done, 63/63 green, verified in Play (two seated at x=2 and
  x=4 ran off along +x at z=-9, logged frame by frame to x=11.7 before despawn, zero gameplay
  errors). Salih's report: they backed out through the queue (`-z`); the clone and the original
  run to whichever screen edge is nearer. `Leave` now offsets along `Mathf.Sign(position.x)`
  (Sign(0) is +1, so the centre slot goes right) and `LeaveDistance` went 6 -> 10 in both the
  struct default and the scene: the camera is ortho 11.5, so a 9:16 portrait frame is ~6.5 wide
  at half-width and the old 6 from x=0 stopped inside it. **Then bent at Salih's call** (his
  sketch: a step up, then a sweep to the side, not a straight line): `Leave` is now a
  `DOPath` Catmull-Rom through `start + forward * LeaveArc` and `start + side * LeaveDistance +
  forward * LeaveArc`, `SetLookAt(0.01)` replacing `TurnTo` because the heading changes all
  along the curve. **Then made an `AnimationCurve` at Salih's call** so the shape is drawn in
  the inspector rather than typed: `ShooterMotion.LeavePath`, x = fraction of `LeaveDistance`
  covered sideways, y = forward offset in world units; `Leave` samples it into 8 points
  (`LeavePathSamples`) for the Catmull-Rom. Default keys (0,0) (0.25,2) (1,2). `LeaveArc` is
  gone. Verified in Play both ways: yaw eased 310 -> 270 leaving left and 50 -> 90 leaving
  right, z rose -10 -> -8 (slot z is -10) and held, x swept to +-12.6. **Scene trap, hit twice
  in this chunk:** a new struct field arrives in the scene as its zero value, and for an
  `AnimationCurve` Unity then writes a linear 0 -> 1 default on the next save - the run looked
  bent but a unit short until the keys were written through `SerializedProperty.
  animationCurveValue` with explicit `Keyframe`s (an eval that referenced the nested
  `ShooterMotion.Defaults` failed silently and printed nothing). **Play-verification trap:** with the
  editor unfocused Play only ticks during a command, so a multi-eval "sample positions" loop
  sees a frozen game; set `Application.runInBackground = true` in the first eval and hook an
  `EditorApplication.update` logger instead of polling. **And unsubscribe it:** a logger installed through eval outlives Play mode, and one that touches scene objects then throws `MissingReferenceException` every editor frame until a domain reload (`EditorUtility.RequestScriptReload()` clears it; hit 2026-09-02). Hook `EditorApplication.playModeStateChanged` to remove it, or read the log and reload.
- `ShotAudio` (Presentation) - done, 63/63 green, verified in Play (five slots chain-firing,
  50 cubes shot, the scene holds exactly 2 AudioSources and the peak count playing at once
  was 2; before, every one of the 40 pooled splashes carried its own source). Salih's report:
  the shot sound stacked into a wall. Cause: `SplashEffect.prefab` had an AudioSource with
  Play On Awake and `Splash.wav`, so the pool re-enabling a splash replayed it per shot,
  uncapped. Fix: the prefab's AudioSource is gone; a scene object `ShotAudio` carries two
  AudioSources (clip, Play On Awake off, 2D) and the component plays them round-robin - `Play`
  on a busy source restarts it, so the voice count IS the concurrency cap and the oldest shot
  is the one cut. The director holds `_audio` and calls `Play()` next to the splash `Take`.
  Rejected: `PlayOneShot` and per-splash sources (both stack one voice per shot with no cap),
  and the project's Max Real Voices setting (it would cap every sound in the game, not this
  one). No test: the whole behaviour is one restart plus a modulo, and EditMode cannot observe
  playback. Add a third AudioSource in the scene if two voices read too thin.
- Spawner hierarchy grouped - done, 63/63 green, verified in Play (`LevelSpawner` holds exactly
  two children, `Cubes` with 100 and `Shooters` with 15). Salih's report: 115 views flat under
  one object. `LevelSpawner.NewGroup(name)` makes an empty child the way `ShotPools.Build`
  does, and each spawn loop instantiates under its own. Shooters stay under `Shooters` while
  they run to a slot and leave; positions are world-space so the parent moves nothing.
  `LevelSpawnerTests` read the board by `GetChild` index on the spawner itself and went red
  with "Transform child out of bounds"; it now reads through `Find("Cubes")`. Tooltips also
  landed on every inspector field the same day (42 across 7 files, struct fields included).
- Spawner settings as structs - done, 63/63 green, verified in Play (100 cubes from -4.28 to
  4.28, 15 shooters from z -14 to -18.5, slot 0 at (-4, 0, -10): every position identical to
  before the move). Same shape as the director's: `Prefabs` (Cube, Shooter), `BoardLayout`
  (Origin, CellSize), `QueueLayout` (Origin, SpacingX, SpacingZ), `SlotLayout` (Z, SpacingX),
  one inspector heading each, public fields with tooltips, a static `Defaults` per layout
  struct. **The defaults are the scene's tuned numbers now** (queue z -14, spacing 2 / 2.25,
  slot z -10), not the stale loose-field initialisers (-7.5, 1.5, 1.2, -5.9) - a fresh spawner
  matches the scene. Scene values re-written through `SerializedObject` under the new paths;
  the eval reported a 5 s main-thread timeout but had applied and saved (check the file, not
  the reply). `LevelSpawnerTests` injected its stand-in prefab by the old path `_cubePrefab`
  and went red in SetUp with a null reference; it now writes `_prefabs.Cube`.
- Layered levels, chunk 1 of 3: format + parser - done, 65/65 green. Salih asked for a
  hard level (3 layers, as many cubes as fit); `BoardModel` already removes top layer first
  but the file format was one layer. `boardRows` became `boardLayers` (a wrapper per layer
  because JsonUtility cannot read `string[][]`, same reason as `shooterColumns`); the ground
  layer sets the size and every other layer must match it in depth and width, refused with
  the location named. `Level_01.json` wrapped in one layer (force-reimported: the editor
  trap above). Red first: 4 tests "The level has no boardRows".
- Layered levels, chunk 2 of 3: `LevelSpawner` - done, 67/67 green. Two silent bugs a
  layered level would have hit: `SpawnCubes` listed layers ground-first while the domain
  removes top-first, so `PopFrontCube` shrank the bottom cube and left the top floating
  (now the inner loop runs `Layers - 1` down to 0 and `CellOfCubeView` inverts to match);
  and `FlowBoardColumn` flowed on every pop, but a row only falls when its whole stack is
  gone - it now returns null unless `_cubeFront % Layers == 0`, the row boundary. The
  director is untouched: it already treats a null slide as "nothing moved". Single-layer
  levels are unchanged (`% 1` is always 0). Red first with the right numbers: the popped
  view sat at y 0.45 where the top was 2.35, and a survivor slid 0.95 on the first shot.
- Layered levels, chunk 3 of 3: `Level_02.json` - done, 67/67 green, verified in Play (600
  cubes, 50 shooters, back row at z 13.8 running off the top of the frame, 3 layers
  stacked to y 2.35, zero errors). Salih's call: 10x20x3 = 600 cubes, the board extends
  backwards past GameArea and the camera; the case's own sample stays `Level_01`. **The
  scene's scope now points at `Level_02`** - swap `_level` back to `Level_01` before the
  case ships. The file is generated, not hand-written: `Docs/Tools/generate_level_02.py`
  simulates the game's own rules (leftmost matching exposed cube, five slots, fail when
  every slot is occupied and targetless), starts from one queue column per colour (always
  winnable: every colour is at a front) and scrambles it with cross-column swaps, keeping
  a swap only while a colour-aware player still wins under 8 random firing interleavings;
  the kept seed (1) wins 40/40 interleavings, and the naive player (always the leftmost
  front) loses. Ammo == cubes per colour (120 each), 6 hidden, columns 9-12 deep. **Every
  stack is one colour** (the three cubes on a cell match; 200 stacks, 40 per colour) -
  Salih's rule after the first cut mixed colours within a stack and read as impossible:
  clearing one cell then needs three different shooters. Re-verified in Play: 600 cubes,
  200 stacks, zero mixed, zero errors. **The
  finding that shaped the generator:** seated shooters strip their own colours off the
  exposed layer, so the exposed set converges to whatever colour is NOT seated - a queue
  whose fronts do not offer every colour often enough is unwinnable however many cubes it
  has. A random queue never won; a queue built from a recorded play only won under the
  exact firing order it was recorded with. Deeper queue rows spawn off-screen behind the
  visible three and step forward as usual.
- A shooter finishes a stack before moving on - done, 67/67 green, verified in Play (a blue
  shooter selected via `OnShooterSelected`, columns losing cubes logged per frame: col6 x3,
  col7 x3, col9 x3, zero errors). Salih's report: on a layered level the shooter killed the
  top cube of one stack, then hopped to ANOTHER matching column, then back. Cause: the
  director held the column for the whole bullet + death + flow (0.27 s) while the fire
  interval is 0.16 s, so the next `TryShoot` saw the column held and `TryFindTarget` skipped
  it to the next match. The hold exists so no shot lands on a cube still sliding into place;
  while a stack still stands nothing slides, so `ShotVisual` now asks the new
  `LevelSpawner.StackStillStands` right after the pop and only holds, flows and releases
  when the row actually falls. With one-colour stacks the leftmost match is then the same
  column shot after shot, which is the original's top-down order. No new test: the domain
  already sticks (no hold in EditMode), the hop lived purely in presentation timing. A
  mixed-colour stack would still hop by design - the domain has no target lock, and the
  level rule above says stacks are one colour.
- Bullets no longer wear the shooter's colour - done, 67/67 green. Salih's call: one
  standard bullet, the prefab's own `Gun.mat`. The director lost `IColorMaterials` entirely
  (its only use was the bullet), so `Construct` is three args and the scope passes
  `materials` to the spawner alone. `CubeView.Wear` stays: the spawner dresses board cubes
  and shooters with it.
- Dying cubes rattle while they shrink - done, 67/67 green, verified in Play (three fronts
  seated and firing, zero errors from the run). Salih's call: a small random positional
  jitter during the swell-and-collapse. `DOShakePosition(ShrinkDuration, ShakeStrength)`
  fired alongside the `DOScale` (not awaited: same duration, ends the same frame; DOTween's
  default fadeOut makes it die down with the scale). `DOPunchPosition` rejected: it is one
  directional hit that springs back, not a random rattle. New `CubeDeath.ShakeStrength`
  (default 0.15, written into the scene via eval - the new-struct-field-arrives-as-0 trap
  again). Vibrato left at DOTween's default 10; add a field if 0.25 s wants more shakes.
- Landing punch is rotation, not position - done, 67/67 green, verified in Play (a column
  flowed: 57 survivors tilted to a 15.0 degree peak and every cube read 0 degrees again
  once settled). Salih's call. `DOPunchPosition(back * SettleDistance)` became
  `DOPunchRotation(Vector3.left * SettleAngle)` - negative x tips the top toward -z, the
  way the cube was travelling, so it reads as inertia. `CubeDeath.SettleDistance` (0.1
  world units) is now `SettleAngle` (15 degrees), written into the scene by hand because
  a renamed struct field arrives as 0. **New rule the swap needed:** the flow's `DOKill`
  used to be healed by the next slide targeting an absolute rest, but a rotation punch
  killed mid-rock leaves the cube tilted and nothing else ever writes its rotation, so
  `FlowBoardColumn` resets `rotation = identity` right after the kill.
- Cube death in three beats - done, 67/67 green, verified in Play (per-frame log of every
  cube: swell peaked at 1.20 = 0.9 x 1.33, rock peaked at 20 degrees, collapse reached
  0.05 before Destroy, everything back to 0.90 between deaths, zero errors). Salih's
  spec: on impact a jelly `DOPunchRotation` (fire-and-forget, `.ToUniTask().Forget()`)
  and a bouncy `DOPunchScale` start together; the `DOScale(0)` collapse starts the moment
  the swell ends (awaited). No `Sequence` - **house rule from Salih: chain tweens with
  `.ToUniTask()` and `await`, or `.Forget()` when nothing waits, never `Append`.** The old
  `InBack` shrink and the parallel `DOShakePosition` are gone with their fields
  (`ShrinkDuration`, `ShrinkOvershoot`, `ShakeStrength`); `CubeDeath` now has `RockAngle`
  20 / `RockDuration` 0.3 / `SwellScale` 0.3 / `SwellDuration` 0.12 / `CollapseDuration`
  0.12, written into the scene by hand (new struct fields arrive as 0). The rock outlives
  the collapse on purpose and is `DOKill`ed right before Destroy. The director's other
  tween awaits were converted to `.ToUniTask()` in the same method; `OnSelected` and
  `Leave` still use the bare `await tween` (same extension, same behaviour).
- Swell exposed as fields, and the vibrato trap - done, 67/67 green, verified in Play
  (one dying cube per frame: x 1.09 -> 0.66 -> 0.90 -> 1.09 -> 0.84, y the mirror image,
  yaw +20 / -14 with four sign changes, then the collapse to 0.05). `CubeDeath.SwellScale`
  is a `Vector3` now ((0.3, -0.25, 0.3): wider and flatter, then the reverse - the
  squash-and-stretch that reads as jelly; uniform reads as a breath), plus `SwellVibrato`
  and `SwellElasticity` (0-1). **The trap, measured before it was understood:** the first
  cut (vibrato 8, 0.35 s, elasticity 1) produced one bump and no bounce at all. DOTween's
  punch cuts itself into `(int)(vibrato * duration)` segments, so vibrato is per SECOND:
  8 x 0.35 = 2 segments = out and back, and the opposite swing that elasticity governs
  only exists from 3 segments up. 20 x 0.35 = 7 segments is the jelly. The rock had the
  same problem (4 x 0.3 = 1, clamped to 2: a single nod) and is now vibrato 15 inline;
  Salih had already switched its axis to `Vector3.up` (a yaw shimmy, not a forward tip)
  and elasticity to 1 by hand, both kept. Tooltips say per-second now.
- `GameLoop.Decided` (Application) - done, 68/68 green. UI chunk 1 of 4. The overlay
  needs to learn the ending without polling, and Presentation/UI are siblings that may not
  see each other, so the loop announces it: `event Action<GameVerdict> Decided`, raised from
  a private `Decide(verdict)` that is now the ONLY writer of `Verdict` past Playing (three
  assignment sites became three calls, so no site can forget the announcement). A plain C#
  event, not an R3 property: Application stays library-free; the ViewModel (chunk 2) is
  where the event becomes R3 for binding. Red first with the compile error, then the test
  pins "raised exactly once, with the verdict, on the SEATING path" - the fail a selection
  causes is the one a TryShoot-only announcement would miss. Next: `LevelEndViewModel`
  (UI, pure C#, R3), then `LevelEndView` (UGUI + restart = scene reload), then the
  director's UniTask loops get a destroy-cancellation token so a fast restart cannot
  touch destroyed views.
- `LevelEndViewModel` (UI) - done, 70/70 green. UI chunk 2 of 4, the first file in
  `Blast.UI`. MVVM per plan D3, Salih's call over MVC: this UI is data display with no
  navigation, binding deletes the controller. Pure C#, ctor takes the `GameLoop` and
  subscribes `Decided`; exposes `ReadOnlyReactiveProperty<bool> IsShown`,
  `ReadOnlyReactiveProperty<string> Title` (`WonTitle` / `LostTitle` consts, written BEFORE
  IsShown flips so a shown overlay is never untitled) and `ReactiveCommand<Unit> Restart`.
  **Restart is a command, not an injected Action** (Salih rejected the callback): the view
  model raises the intent, Bootstrap subscribes and decides it means a scene reload, so the
  view model never sees Unity and the test never needs a fake. No test on Restart itself -
  it is `new ReactiveCommand`, nothing in this file can break it. No Dispose: loop and view
  model are both level-lifetime and die together on the reload. R3 core (`R3.dll`) comes
  from NuGetForUnity under `Assets/Packages`, auto-referenced, so `Blast.UI.asmdef` needed
  nothing; the test asmdef overrides references and got `R3.dll` + `Blast.UI` explicitly.
  Red first: `'UI' does not exist in the namespace 'Blast'`.
- `LevelEndView` + scene overlay + scope wiring (UI, Bootstrap) - done, 71/71 green,
  verified in Play (forced `Decide(Won)` via reflection: panel off -> on, title "Level
  Complete"; `RestartButton.onClick.Invoke()` reloaded the scene: 1 scope, verdict Playing,
  panel off, 600 cubes, zero console errors). UI chunk 3 of 4. The view is humble: three
  serialized refs (`_panel`, `_title` TMP, `_restart` Button), `Construct(viewModel)` binds
  IsShown -> SetActive, Title -> text (both `AddTo(this)`) and onClick -> `Restart.Execute`.
  Plain `onClick.AddListener` over `OnClickAsObservable`: the button dies with the view, so
  no subscription to manage. `LevelEndViewTests` builds the hierarchy in EditMode and
  asserts each binding by name (a forgotten binding is the only silent mistake a humble
  view can make). The scope now builds `LevelEndViewModel(loop)`, subscribes `Restart` to
  `SceneManager.LoadScene(gameObject.scene.buildIndex)` (restart = reload; the composition
  decision lives here alone) and hands the view model to `_levelEnd`. The director's
  `AnnounceIfDecided` + `_verdictAnnounced` are gone. Scene: `Canvas` (overlay, scaler
  1080x1920 match 0.5) > `LevelEnd` (view) > `Panel` (60% black, inactive) > `Title`
  (Baloo2 110) + `RestartButton` (520x160 amber, "RESTART" label), built by
  `eval_file` and saved. `Blast.UI.asmdef` gained R3.Unity, Unity.TextMeshPro,
  UnityEngine.UI; the test asmdef the latter two. **Capture trap:** `capture_game_view`
  renders the camera only, a Screen Space Overlay canvas is invisible in it; `unity cmd
  screenshot` (ScreenCapture) shows the overlay. Next, chunk 4: destroy-cancellation
  tokens on the director's UniTask loops so a restart mid-flight cannot touch destroyed views.
- Shooters idle when the level is decided - done, 71/71 green. Salih's report (video):
  after a fail the last shooter to fire stayed frozen in `Runner_Shoot`. Cause: `FireLoop`
  returned on a decided verdict without the `SetRunning(false)` a targetless tick would
  have done, and `PlayShoot` had dropped `isIdle`, so no transition ever left Shoot. The
  verdict exit now calls `SetRunning(false)` + `FaceForward` before returning. Verified in
  Play with forced `Decide(Lost)` (reflection) mid-fire: five probes, every seated shooter
  sampled `WalkingCube_Idle` with `isIdle=True`; a per-frame logger showed each
  `Shoot idle=True` sample resolving to Idle after exactly the 0.25 s Any->Idle blend, and
  a Shoot trigger set during an un-interruptible blend firing once afterwards, then
  consumed. One probe out of six sampled two seated shooters still in Shoot 1.5 s after
  the forced Lost and did not reproduce; not explained. If it recurs in real play, log
  `isIdle` / `isRun` / the `Shoot` trigger per frame for the stuck view (the eval logger
  pattern: `EditorApplication.update`, unsubscribed on `playModeStateChanged`).
- Director awaits cancel on destroy - done, 71/71 green, verified in Play. UI chunk 4 of
  4. Red first, live: shooters firing, `Decide(Won)` + `RestartButton.onClick.Invoke()` in
  the same frame gave 2 x `MissingReferenceException: 'UnityEngine.Animator' has been
  destroyed` (FireLoop ticks waking after the reload) plus 2 errors caught by DOTween's
  safe mode. Fix: `_destroyed = this.GetCancellationTokenOnDestroy()` in `Awake`, passed to
  every await in the file - `UniTask.Delay(..., cancellationToken:)` and every tween's
  `.ToUniTask(cancellationToken:)` (the two bare `await tween`s in `OnSelected` and `Leave`
  became explicit `ToUniTask` calls for it). A cancelled await throws
  OperationCanceledException into a `UniTaskVoid`, which UniTask drops silently by default,
  so nothing after the await runs and nothing is logged. Same probe after: 28 cubes shot,
  restart, 600 cubes back, 0 errors. No EditMode test: the behaviour is UniTask's
  cancellation, observable only with a scene reload. **Console-reading trap, hit here:**
  the CLI `console` entries carry `level` (`error`) and `timestampUtc`, not `type`; the
  earlier "zero console errors" reads in this section that filtered on `type` saw nothing
  and would have said zero regardless. Filter on `level` and on a timestamp taken before
  the probe. The UI is complete: overlay, restart, frozen shooters, clean reload.
- Outline on selectable shooters - done, 72/72 green, verified in Play (5 outlined at the
  queue's front row z=-14 and 46 plain; after a tap the popped shooter ran to its slot
  plain and the one stepping up wore the outline; zero errors). Juice 1. Apps'
  `Cube_Outline.mat` is an inverted-hull URP Shader Graph (`OutlineShader`, RenderFace
  Back, `_Color` black, `_Scale`) meant as a SECOND material slot on the same mesh, so
  `ShooterView.SetOutlined(bool)` writes `sharedMaterials` as `[colour, outline]` or
  `[colour]` on every coloured part (MeshRenderer + SkinnedMeshRenderer both draw twice).
  Slot 0 stays the colour, so `Wear` (which sets `sharedMaterial`) keeps working in either
  order. The outline material is a prefab field (`_outline`, wired to `Cube_Outline` via
  eval), not a palette row: it is how a shooter looks selectable, not a colour. The
  spawner owns the rule "outline = you may tap this": `SetOutlined(depth == 0)` at spawn,
  `false` in `PopFrontShooter`, `true` on the view revealed in `StepQueueForward`.
  `LevelSpawnerTests.Outline_FollowsTheSelectableFront` builds the first ShooterView
  stand-in (MeshRenderer + a 3D TextMeshPro child + a throwaway material; the animator can
  stay null because Construct never calls SetRunning) and asserts front outlined / second
  not / popped plain / stepped-up outlined. Red first: 71/72. One small array per toggle,
  on a tap, not per frame. Outline thickness is `_Scale` on the material, Salih's to tune.
- Slot markers on the dock - done, 73/73 green, verified in Play (5 markers at x -4..4,
  z -10, sprite visible, zero errors). Juice 2, first half of "deck + framing". What Apps
  supplied for this: `Textures/Slot.png` (488 px sprite, unused until now) - the rounded
  translucent square the original draws under each shooter slot. NOT a deck: the scene's
  `Gate` (Gate.fbx at scale 320, `GameArea_Ceiling`) is the canopy over the board's far
  edge, and a Gate.fbx at scale 1 is 0.1 units wide. `Prefabs/SlotMarker.prefab` is a
  SpriteRenderer (Slot.png, rotated 90 on X, scale 0.35 = 1.71 world units against the 2.0
  slot spacing), no material of its own. The spawner spawns one per slot under a `Slots`
  group at `SlotWorldPosition(slot)`: the count is the level's (`SlotRow.Slots`), so a
  four-slot level shows four, and a marker can never drift from the position the shooter
  is actually seated at because both read the same formula. `LevelSpawnerTests` gained a
  stand-in marker in SetUp (an empty Transform; the earlier spawner tests went red in
  SetUp without it, 68/73) and one test: count follows the level, each marker is under its
  slot. Framing itself (the empty band between board and dock) is untouched: those are
  Salih's numbers (slot z -10, queue z -14, camera ortho 12 at (0,10,-10) / 70 deg).
- Framing matched to the original + brief re-read - done, 72/72 green, verified in Play.
  Salih re-sent the case deck and a YouTube frame of the shipped game (375x812) as the
  proportion reference; the deck's own images are schematic wireframes, not to scale (their
  phone frame is 0.63 wide, and they put the slot row at 53%), so the frame outranks them.
  Measured, as % of screen height / width: original board width 88%, board bottom 50%,
  slot row centre 64%, slot spacing 18.4%, shooter rows 75/85/92%; ours before was 88 /
  44 / 64.5 / 18 / 77-86-95. Horizontal scale and the dock already matched; the one real
  gap was the board sitting 6 points too high, a 20% board-to-dock band against 14%. Fix:
  the board moved 1.5 units back in WORLD, not the camera, so the dock and queue kept their
  screen positions: `GameArea` z 0 -> -1.5, `Gate` (the canopy at the far edge) z 4 -> 2.5,
  `_boardLayout.Origin` z -4.275 -> -5.775 in the scene and in `BoardLayout.Defaults`. After:
  board bottom 49.3-50.0%, top 13% (original ~9%: the shipped game's camera is perspective
  and renders the board ~4% taller for the same width; left alone, ortho stays).
  **Brief facts re-confirmed from the deck (they override anything below that disagrees):**
  Not1 grid always 10x10; Not2 slot count ALWAYS 5; Not3 shooter column count is the
  level's; Not4 exactly 2 queue rows visible beyond the selectable one; slide 4 says the
  selectable row is "more stroked" via `Cube_Outline` as a secondary material (done that
  way); slides 8-9 want the background darkened and the words **WIN** / **LOST** plus a
  restart button - `LevelEndViewModel.WonTitle` / `LostTitle` are now "WIN" / "LOST"
  verbatim (the test compares against the consts, so it did not move). Because the count is
  fixed at five, the spawned slot markers went again (Salih: put the five in the scene):
  `Dock` holds five `SlotMarker` prefab instances at x -4..4, z -10; `SpawnSlotMarkers`,
  `Prefabs.SlotMarker`, the test and its stand-in are deleted. If the slot z ever moves,
  move the Dock by hand. **Salih then tuned the markers by hand (2026-09-02): z -9.58, scale 0.31 -
  they sit exactly under a seated shooter. Do not touch them.** **The scope now boots `Level_01`** (the case's own level; the
  10x20 `Level_02` stays in the repo as the hard level, swap it in by hand to stress-test).
- Ammo counter punch - done, 72/72 green, verified in Play (one seated shooter logged per
  frame through ten shots: counter scale base 1.000, peak 1.300, back to 1.000 at the end,
  no drift, zero errors). Juice 3. `ShooterView.SetAmmo` now does `DOKill(complete: true)`
  on the counter's transform and then `DOPunchScale(one * _ammoPunchScale, _ammoPunchDuration,
  vibrato: 2)`; two new serialized fields (0.3, 0.15 s) with tooltips, prefab keeps the C#
  defaults (a new plain field on a MonoBehaviour arrives with its initialiser - only fields
  inside an existing serialized STRUCT arrive as zero). Why complete-then-punch: a punch
  returns to the scale it started from, so killing one mid-swell and starting the next from
  there would grow the counter shot by shot. Vibrato 2 x 0.15 s = one segment: out and back,
  a tick. No test: a tween on a humble view. Not awaited, not UniTask-wrapped: nothing waits
  for it.
- Landing squash on arrival at the slot - done, 72/72 green, verified in Play (root scale
  logged per game frame: peak (1.19, 0.76, 1.19), a 1.05 rebound, back to 1.000; zero
  errors). Juice 4. `OnSelected` fires `DOPunchScale(_motion.LandSquash, _motion.LandDuration,
  vibrato: 10, elasticity: 0.3)` on the view's root right after the run, not awaited (the
  first shot leaves mid-squash, as in the original). New `ShooterMotion` fields `LandSquash`
  (0.2, -0.25, 0.2: wider and flatter, the cube swell's shape) and `LandDuration` 0.3 -
  written into the scene by eval, the new-struct-field-arrives-as-zero trap again.
  **Measured trap that set the numbers:** the first cut (0.2 s, vibrato 15, elasticity
  0.5) showed NO squash at all, only the stretch swing: the arrival frame carries a 56 ms
  hitch (the first shot's splash + audio + bullet on the same frame, editor first-use
  cost) and the whole 0.067 s squash segment fell inside it, leaving the 0.5-elastic
  rebound as the only visible motion - the opposite shape. Now 3 segments of 0.1 s and a
  0.3 rebound. Checked and cleared: the WalkingCube Animator is on a child at scale 1.91
  and never writes the root scale. Logger pattern reused (`EditorApplication.update`,
  one sample per `Time.frameCount`, unsubscribed on `playModeStateChanged`).
- Overlay entrance - done, 72/72 green, verified in Play (per-frame log from a decision
  raised inside a normal frame: panel alpha 0 -> 1 by 0.27 s, title scale 0 -> 1.10 overshoot
  at 0.22 s -> 1.00 at 0.38 s; zero errors). Juice 5. `LevelEndView.Show(bool)` replaced the
  inline `SetActive` lambda: it still switches the panel, then resets and tweens a
  `CanvasGroup` alpha 0 -> 1 (`_fadeDuration` 0.3) and the title's scale 0 -> 1 with `OutBack`
  (`_titlePopDuration` 0.4), both fire-and-forget, both reset first so a re-show never starts
  half-faded. The fade is `DOTween.To` on `alpha`, NOT `CanvasGroup.DOFade`: that extension
  is in DOTween's UI module under `Plugins/Demigiant/DOTween/Modules`, which has no asmdef
  and compiles into Assembly-CSharp-firstpass, unreachable from `Blast.UI`. The scene's
  `Panel` gained a CanvasGroup, wired to `_panelGroup`. `LevelEndViewTests` adds the group
  to its stand-in and, after `DOTween.CompleteAll()`, asserts alpha 1 and title scale one -
  the silent failure being an overlay that is active and invisible. No red-first on this
  one: the asserts were written with the code (they pin the END state, which a forgotten
  tween target would break). **Measurement trap, new:** a decision forced from inside an
  eval lands on a frame the eval itself stretched; Unity clamps that frame to
  `maximumDeltaTime` 0.333 s and DOTween advances the whole 0.3/0.4 s entrance in it, so
  the first sample already showed everything finished. Raise the event from the logger's
  own Nth `EditorApplication.update` tick instead, then sample.
- Level progression, chunk 1 of 4: Easy Save 3 wired into the layering - done, 72/72 green.
  Salih installed ES3 (`Assets/Plugins/Easy Save 3`, source, plus `ES3_TMPRO` / `ES3_UGUI`
  defines in ProjectSettings) for "resume the last level". Plugins scripts without an asmdef
  compile into Assembly-CSharp-firstpass, which no asmdef assembly can reference, so ES3's
  own support was used: `Tools > Easy Save 3 > Enable Assembly Definition Files` renames its
  shipped `EasySave3.asmdef` / `EasySave3Editor.asmdef` into place (references
  Unity.VisualScripting.Core + Unity.TextMeshPro; the Editor one is Editor-only), and
  `Blast.Infrastructure.asmdef` now references `EasySave3`. **Trap:** that menu command
  popped a modal and froze the main thread for minutes (every main-thread command timed
  out at 30 s while `console` and `recompile_status` still answered); Salih closed it.
  Design agreed: `ISaveStore` (Application, `T Load<T>(key, fallback)` / `Save<T>(key,
  value)`, SYNC on purpose - local only, no PlayFab or any cloud; async would drag UniTask
  into Application and an async boot into the scope for nothing) implemented by
  `Es3SaveStore` (Infrastructure); `LevelProgression` (Application) owns the index and the
  key; the scope holds `TextAsset[] _levels` and boots `_levels[progression.Current]`;
  `loop.Decided += progression.Record` (Won advances and wraps, Lost holds; saved at the
  verdict, not at the button); the overlay's button reads NEXT after a win and RESTART
  after a loss, and still just reloads the scene.
- Level progression, chunk 2 of 4: `ISaveStore` + `LevelProgression` (Application) - done,
  76/76 green. `ISaveStore` is two generic methods, `T Load<T>(key, fallback)` / `Save<T>(key,
  value)` - an interface because the backend is a dependency supplied from outside and
  substituted in tests (the test's `MemoryStore` is the whole contract in eight lines).
  `LevelProgression(levelCount, store)` reads `"LevelIndex"` in its ctor folded by `%
  levelCount` (a save from a longer catalogue must not throw on boot after a level file is
  removed - the fourth test), `Record(Won)` advances, wraps and saves, `Record(Lost)` returns
  early. Red first with the compile error. The extension point Salih asked for is the
  interface, not an abstract base: ES3 and any other local store share a contract, not
  code; a base class appears the day two stores share a line.
- Level progression, chunk 3 of 4: `Es3SaveStore` (Infrastructure) - done, 76/76 green. Two
  expression-bodied methods over `ES3.Load(key, fallback)` / `ES3.Save(key, value)`; the file
  and format are ES3's defaults. No test: one call each way, file IO. Swapping the backend
  is another ISaveStore class plus one `new` in the scope.
- Level progression, chunk 4 of 4: the loop wired - done, 76/76 green, verified in Play
  (ES3 key cleared, then: fresh boot Level_01 100 cubes / forced Won -> panel NEXT, saved
  1 / click -> Level_02 600 cubes / forced Lost -> RESTART, saved 1 / click -> Level_02
  again / forced Won + click -> Level_01, saved 0, the wrap; zero errors; key cleared
  again afterwards so Salih and the reviewer boot Level_01). `GameLifetimeScope` lost
  `_level` for `TextAsset[] _levels` (scene: Level_01, Level_02), builds `new Es3SaveStore()`
  - the one swap line - and `new LevelProgression(_levels.Length, store)`, parses
  `_levels[progression.Current]`, and subscribes `loop.Decided += progression.Record`.
  `LevelEndViewModel` gained `ButtonLabel` (`NextLabel` "NEXT" after a win, `RestartLabel`
  "RESTART" after a loss); the view binds it to the button's TMP label (`_buttonLabel`).
  The button still only reloads the scene: what the reload brings was decided at the
  verdict. Tests extended, red first with the compile error. **ES3 file:** lives in
  `Application.persistentDataPath`, shared between Play sessions on one machine; to
  boot Level_01 again run `ES3.DeleteKey("LevelIndex")` in an eval or
  `Tools > Easy Save 3 > Clear Persistent Data Path`. The reviewer's fresh machine has no
  file, so index 0.
- **Reverted to the brief on 2026-09-03, 76/76 green:** slides 8-9 say "restart the same
  level" after a WIN too, and `.claude/rules/case-brief.md` now outranks everything, so
  the scope no longer subscribes `loop.Decided += progression.Record` and
  `LevelEndViewModel.ButtonLabel` is always `RestartLabel` (`NextLabel` deleted). The
  progression stack (`LevelProgression`, `ISaveStore`, `Es3SaveStore`, `TextAsset[]
  _levels`) is still built and read at boot but nothing ever advances it, so it is dormant:
  a fresh machine always boots `_levels[0]` = `Level_01`. Salih's decision on 2026-09-03
  was that the stack stays as the seam a level select would be stitched onto.
  **Reversed by Salih on 2026-09-04 and the whole stack is deleted - see the next entry.**
- **Save/load deleted, 84/84 green** (88 minus `LevelProgressionTests`), verified in Play
  (`Level_01`, 100 cubes, 15 shooters, zero errors). Salih reopened his own 2026-09-03
  decision after weighing a NEXT button for the win overlay and rejecting it: with one
  shipped level the stored index can never leave zero, so `LevelProgression`, `ISaveStore`,
  `Es3SaveStore` and Easy Save 3 were 194 files and 2.1 MB serving **two lines** that
  always answered 0 - "no config for a value that never changes". The new evidence that
  settled it: ES3 is the only code in the project that does not compile on a newer editor,
  and it was the sole cause of the Safe Mode that morning. Gone with it: the `EasySave3`
  asmdef reference, the `ES3_TMPRO` / `ES3_UGUI` defines on all 16 platforms, and the
  plugin patch that fixed its 16 B/frame idle coroutine (`Docs/PERFORMANCE.md` step 3
  keeps the finding; the allocation itself left with the plugin). `GameLifetimeScope`
  holds one `TextAsset _level` now instead of `TextAsset[] _levels` - **a serialized path
  change, so the scene reference was rewritten through `SerializedObject`** (the same trap
  as every struct-field move). Side benefit: the scene's `_levels` had been hand-edited
  down to `Level_03` alone for the perf screenshots, which would have shipped the 900-cube
  stress level to the reviewer; it now boots `Level_01` and cannot drift again. To profile
  on `Level_02` / `Level_03`, swap that one field - no ES3 file to push to the phone. Both
  files stay in `Assets/00_GAME/Levels`, still checked by `LevelFileTests`, still the
  evidence behind the performance ledger. **Do not reintroduce a save system for the
  case:** the brief ships one level and replays it after a win as well as a loss.
- Bullet is Apps' `Gun_Sprite` streak, aimed on Take - done, 76/76 green, verified in Play
  (five fronts seated, 100 -> 50 cubes; per-frame log of two in-flight bullets:
  dot(forward, velocity) 1.000 on every sample, the sprite's up along the velocity, its
  normal (0,-1,0); zero gameplay errors). Salih's find: `Gun_Sprite.png` (125x231, a round
  head with a fading tail) IS the bullet's streak, not a hint for a TrailRenderer. He
  rebuilt `Bullet.prefab` by hand: root still the Cube.fbx head in `Gun.mat` at scale 0.4,
  rotation identity now (was -90 X); a `Trail` child SpriteRenderer (URP's
  `Sprite-Unlit-Default`, +90 X so the quad lies flat on the floor with the head toward
  root +Z, local z -1, scale 2); TrailRenderer gone. Director: `_pools.Bullets.Take(muzzle,
  Quaternion.LookRotation(aim))` - the splash's aim through the existing overload, one line;
  the prefab-rotation `Take` would have flown the streak sideways. `Bullet_Trail.mat` deleted
  (its only user was the TrailRenderer). Why sprite over trail: a TrailRenderer rebuilds and
  uploads a mesh per bullet per frame (MinVertexDistance 0, 90 corner + 90 cap vertices), one
  unbatchable draw each, and a pooled one keeps its old positions, so a reused bullet streaks
  from the last impact to the new muzzle unless `Clear()` is called - the "watch the first
  reused shot" item above, now moot. The sprite is one static quad, every bullet batches,
  and the tail exists from the first frame (a 0.11 s trail barely formed in a 0.12 s
  flight). Not seen by eye: the probe's screenshot landed on the frame the muzzle splash
  covers the bullet; the log is the evidence. **Probe trap:** `Level_01`'s front row is one
  colour, so seating a single front never fires - seat all five.
- Held columns removed - done, 74/74 green, verified in Play (five fronts seated, 47 shots,
  100 -> 53 cubes, zero gameplay errors). Salih checked the original: a shooter fires at the
  next cube while the front one is still dying, it never waits for the row to settle, so the
  hold from 2026-09-02 (his own earlier request) is reverted. Gone: `GameLoop._held` /
  `HoldColumn` / `ReleaseColumn`, the `GameRules.TryFindTarget` overload with the mask, the
  two `GameLoopTests` that pinned them, the director's hold / await-slide / release, and
  `LevelSpawner.StackStillStands` is private again (only `FlowBoardColumn` reads it).
  `FlowBoardColumn` still returns the slide but nothing awaits it. The stack-lock behaviour
  ("a shooter finishes a stack before moving on") survives without the hold: the domain's
  leftmost match is the same column shot after shot. **Measured the one risk:** a bullet
  flies to the cube's position at fire time, and the cube can slide during the 0.12 s flight
  - across 47 shots (50 samples with the target mid-slide) the tween's `endValue` sat at
  most 0.28 units from the cube's centre, mean 0.04, inside a 0.9-wide cube; no retargeting
  needed. **Probe trap:** an `EditorApplication.update` logger can tick every ~8 game frames
  under load, so "last position while active" was mid-flight; read the DOTween
  `TweenerCore.endValue` instead. Also: an `eval_file` that reports the 5 s main-thread
  timeout may still have run - check the console before installing a second copy.
- Settling columns as a preference, not a ban - done, 75/75 green, verified in Play (five
  fronts seated on `Level_01`; death order logged by the frame a cube view is destroyed:
  the first ten were c0 c5 c1 c6 c2 c7 c3 c8 c4 c9, all at the front line z -5.78 - red and
  blue alternating, each sweeping its half 0-1-2-3-4; the second red row then died at the
  front line too, i.e. settled; yellow, whose only match was column 0, fired at it while it
  was still one cell back - the fallback; zero gameplay errors). Salih's 4x2 case: after the
  leftmost front dies the shooter should sweep the other fronts before coming back for the
  cube behind, but never wait when that cube is the only match. `GameLoop._settling` +
  `MarkSettling` / `MarkSettled`; `GameRules.TryFindTarget(board, color, settling, out)`
  takes the leftmost NON-settling match and otherwise the leftmost settling one - one pass,
  one remembered index, no allocation; the 3-arg overload forwards null. Director: mark on
  the pop when the row falls (`StackStillStands` public again), await `FlowBoardColumn`'s
  slide, unmark. Rejected: a time threshold (the old ban with a shorter fuse - still waits
  when there is no other target) and a neighbour-first scan (a different rule that only
  fits 4x2; cost was never the issue, ten columns per shot). `IsFailed` is untouched and
  no longer interacts with the mask: a settling column is always a legal target. Test:
  `Shoot_PrefersASettledColumnButNeverHoldsFireForOne` - the second half is what the old
  hold would have failed. Red first with the compile error. **Trap:** an EditMode test run
  hangs at "running" forever while the editor is in Play mode (Salih had pressed Play);
  `cancel_tests` + `editor_stop`, then rerun. **Probe trap:** `CubeView` roots are not at
  scale 0.9 (the 0.9 sits on the mesh child), so "scale != 0.9 means dying" flagged all 100.
- Column states: Locked, then Settling, then Free - done, 78/78 green, verified in Play
  (five fronts seated on `Level_01`, 50 shots, 100 -> 50 cubes; the gap between consecutive
  bullets aimed at the same column, read off each bullet's tween `Elapsed`, was never under
  0.368 s; zero gameplay errors). Salih's finding, from 4 orange in front of 4 blue: the
  domain removes a cube at fire time, so the blue behind became a target the same instant
  the orange in front was shot - the blue shooter fired before the orange bullet had even
  landed. The settling preference alone cannot stop that, so the column now walks three
  states, `Domain/ColumnState.cs`: **Locked** from the pop until the swell ends (flight +
  hit, 0.24 s: no target at all, `GameRules` does not even remember it), **Settling** from
  the collapse until the slide lands (a last resort, as before), **Free** after.
  `GameLoop.LockColumn` / `MarkSettling` / `MarkSettled` are COUNTS per column
  (`_lockedShots`, `_settlingShots`), and `RefreshState` derives the `ColumnState[]` that
  `TryFindTarget` reads. **Why counts, measured before it was understood:** with plain
  flags the first probe showed 4 of 50 same-column gaps at exactly one flight (0.12 s):
  shot A's slide finished and freed the column while shot B's bullet was still in the air,
  because A's `MarkSettled` overwrote B's lock. The test
  `AColumnShotTwice_StaysLockedUntilBothShotsHavePlayedOut` went red first with the right
  message ("The first shot settling freed a column the second shot still has locked").
  Also back: `AFullRowWaitingOnALockedColumn_IsNotLost` - `IsFailed` reads the bare board,
  a locked column's cubes are on their way. Stacks still get no states while they stand
  (the next cube is right under the dying one). **Console trap, hit here:** `unity cmd
  console` returns old entries too, so a grep for a probe's tag matched the PREVIOUS run's
  lines and hid that the new eval had never installed; filter on `timestampUtc` against a
  stamp taken before the probe (`since.py` pattern) before believing any probe output.
- Cube death re-timed to the original, measured frame by frame - done, 78/78 green, verified
  in Play (five fronts seated on `Level_01`, 49 cubes died in 3.5 s, zero errors; one dying
  cube logged per frame: scale 0.79 -> 0.53 -> 0.34 -> 0.20 -> 0.09 -> 0.01 over 13 frames
  = 0.18 s, hop peaking at y +0.15 and back down by 0.1 s). Salih sent a 2 s YouTube clip of
  the shipped game (30 fps content in a 60 fps container, every frame doubled, so +-33 ms on
  every number); frames were extracted with ffmpeg, the yellow mask measured per column per
  frame, and the sheets kept locally as `Docs/Screenshots/original_cube_death_frames.png`
  (column 0 at 4x) and `original_shot_sweep_frames.png` - that folder is gitignored, so they
  are not in the repo. **What the original does, impact = t0:** the cube
  HOPS up once (a shadow wedge appears under it, one frame) and shrinks to nothing IN PLACE,
  fast first and slow last (0.85 -> 0.58 -> 0.37 -> 0.16 -> gone in ~150-190 ms; OutQuad and
  OutSine both fit at this sampling rate), with a white splash ON THE CUBE as well as the
  muzzle puff; the survivors do not move until the dead cube is gone; then the column slides
  one cell in ~120 ms near-linearly, overshoots ~10-12% of a cell in the travel direction and
  settles ~100 ms later (a single `OutBack` fits within the noise, see the next chunk); the
  shooter fires every ~115 ms and sweeps c0..c4 before returning to c0, which is our
  Locked/Settling preference producing the same order. **What it does NOT do:** no yaw rock,
  no jelly swell, no landing tilt, no 470 ms of wobble. So `ShotVisual`'s death is one beat
  now: `DOPunchPosition(up * HopHeight, HopDuration, vibrato 1)` fire-and-forget alongside an
  awaited `DOScale(0, CollapseDuration).SetEase(OutQuad)`, and a second `Splashes.Take` at the
  cube's position before it. Gone with their fields: `RockAngle`, `RockDuration`,
  `SwellScale`, `SwellDuration`, `SwellVibrato`, `SwellElasticity`. New: `HopHeight` 0.15,
  `HopDuration` 0.1, `CollapseDuration` 0.18 (written into the scene via eval; new struct
  fields arrive as 0). `MarkSettling` moved to after the shrink, its only remaining await, so
  Locked now lasts flight + 0.18 s. Two splashes per shot pushed the pool past its prewarm
  (peaked at 41 with Salih's 0.1 s interval), so `_splashes.Prewarm` is 48 in the scene and
  the C# default. No new test: a tween chain on a humble view, no branch. **Dialog trap, new:**
  an eval that dirties the scene while an EditMode run is starting makes the test runner pop
  "Scene(s) Have Been Modified", which freezes every main-thread command; Salih clicked Save.
  Write scene values and start tests in separate steps. The fit script and the per-frame
  measurements live only in this session's scratchpad; the numbers above are the record.
- Column flow as one OutBack - done, 78/78 green, verified in Play (five fronts seated on
  `Level_01`, 45 cubes shot, zero errors; one survivor logged per frame across three slides:
  -1.28 -> crosses the rest -2.18 at ~0.11 s -> peaks -2.264 at 0.18 s, 9.3% of a cell past ->
  settled by 0.3 s; the other two slides peaked 0.085 past their rests too; max rotation on
  any cube 0.00 degrees). The measured original slide (linear ~120 ms + a ~10% overshoot
  settling ~100 ms later) was fitted against three candidates: linear + position punch
  (rms 0.086), OutBack (rms 0.100, T 320 ms, DOTween's default overshoot 1.7), InBack
  (rms 0.149, no fit: it pulls back first and never overshoots). One frame at 30 fps is
  ~0.25 of a cell of progress, so the first two are indistinguishable and the single tween
  wins - Salih's call. `FlowBoardColumn(column, duration, overshoot)` is now one
  `DOMove(rest, duration).SetEase(Ease.OutBack, overshoot)` per survivor after the `DOKill`;
  the `OnComplete` closure, its `ponytail:` marker, `DOPunchRotation` and the
  `rotation = identity` reset are gone (nothing rotates any more, so nothing needs healing).
  `CubeDeath.SettleAngle` / `SettleDuration` became `SettleOvershoot` 1.7, `FlowDuration` is
  0.3 (the whole slide, overshoot and settle included); both written into the scene via eval.
  OutBack's known difference from the original: its peak sits ~50 ms later and the return
  ~100 ms later, a slightly floatier landing; if it reads that way, drop `FlowDuration` to
  0.25 and raise `SettleOvershoot` to ~2.5. `LevelSpawnerTests` only changed its four call
  sites to the new signature; the end state it asserts (rest reached after `CompleteAll`) is
  what OutBack guarantees at u = 1. **Probe trap:** a frame logger that only records cubes
  whose z changed since the last tick never logs the first frame, so the first sample is
  already mid-slide; read the rest from the authored grid, not from the first line.
- Hop deleted - done, 78/78 green. Salih could not see it (2 segments of 0.05 s, 0.15 units,
  under a 70-degree camera, during the shrink) and typing 12321 into `HopDuration` froze the
  editor (cause not measured; a punch of that length is 12321 segments per dying cube).
  The death is one awaited `DOScale(0, CollapseDuration).SetEase(OutQuad)` now, the
  explicit `DOKill` before `Destroy` went with it (nothing else tweens the cube), and
  `CubeDeath` is three fields: `CollapseDuration`, `FlowDuration`, `SettleOvershoot`. The
  original's one-frame lift stays recorded above as a fact, not a feature.
- Fire interval 0.12 s, the original's rhythm - done, 78/78 green. Measured from the clip:
  ammo ticks at 97 / 97 / 128 / 115 / 85 ms, mean ~114 ms. `_firing.Interval` was 0.1 in
  the scene (Salih's hand tune) and 0.22 in `Firing.Defaults`; both are 0.12 now, and the
  defaults also took the scene's `MuzzleHeight` 0.5 / `FlightDuration` 0.17, so a fresh
  director matches the scene (the rule from the spawner-settings chunk).
- `CameraShake` (Presentation) - done, 80/80 green, verified in Play (five fronts seated,
  camera offset logged per frame: peaks exactly 0.050, back to 0.000 in ~6 frames, restarts
  cleanly on every impact at the 0.12 s rhythm, zero errors). The original's impact tick,
  measured on the same clip: the untouched columns' edges move 1-2 px on a 37 px cell right
  after a hit, i.e. ~0.03-0.05 world units for a frame or two. Salih's call, and his idea:
  one `DOShakePosition(duration, amplitude, vibrato)` built on the first `Kick`, kept alive
  with `SetAutoKill(false)`, and `Restart`ed on every impact - DOShake allocates its segment
  arrays per CALL, so a shake per shot at ~40 impacts/s would be a steady allocation; the
  reused tween costs nothing after the first kick, and Restart snaps to the rest before
  shaking again so overlapping kicks never stack. Every shake follows the same pattern; at
  a twentieth of a unit it cannot be told. Rejected: Cinemachine impulse (a Brain driving
  the camera every frame plus a package for a 2 px wobble; Salih had added 3.1.7 to the
  manifest, removed again - the removal cost a ~4 min "Hold on..." package resolve during
  which every pipeline command timed out at 30 s, so it looked like the dialog trap but was
  not) and a hand-written `LateUpdate` decay (more lines for the same zero allocation).
  Lives on `Main Camera`; the director holds `_shake` and calls `Kick()` next to the impact
  splash. Fields: `_amplitude` 0.05, `_duration` 0.12, `_vibrato` 20 (per second, so 2
  segments). The start position is captured on the first kick, so the camera must be at
  rest then - it always is, the rig is static. `CameraShakeTests`: a second kick after a
  finished shake still plays (red first as a compile error, then red again with
  `SetAutoKill(false)` removed: 79/80), and a finished shake leaves the camera at rest.
- **Performance pass, step 1: baseline** - done, 80/80 green. The ledger is
  `Docs/PERFORMANCE.md` (targets, device, config-as-found, plan, every step's before/after);
  the case study's performance section is written from it, and each later step is recorded
  there, not here. Instrument: `PerfProbe` (Presentation), a `ProfilerRecorder` logger, one
  line per second (frame ms, main/render thread ms, GC B/frame, batches, SetPass, draws,
  shadow casters, tris), `Debug.isDebugBuild`-gated so a release build never ticks it; scene
  object `PerfProbe`. No test: diagnostics reading Unity counters, nothing reachable in
  EditMode. Reference phone: Samsung Galaxy A16 (Helio G99, Mali-G57), driven over adb
  (`input tap` seats shooters, `logcat` returns the lines). Stress level `Level_03` (10x30x3
  = 900 cubes, 100 shooters, `Docs/Tools/generate_level_03.py`, no scramble, winnable by
  construction) is the third `_levels` entry; **the game still boots `Level_01`**, the
  stress level is reached by writing `LevelIndex` 2 into the ES3 file (on the phone: push
  `SaveFile.es3` to `/sdcard/Android/data/com.APPS.CaseStudy/files/`). Findings: the phone
  runs at **30 fps** (`targetFrameRate` -1), `Level_03` is 36 ms/frame with 50 ms drops
  before anyone taps, one 16 B allocation every frame even idle, every cube is two draws
  (main + shadow), SetPass 11-14 so the SRP Batcher is fine. **Editor counters are not
  usable for render numbers** (they include the Scene view: 925 batches constant while
  cubes died) nor for GC (28 KB/frame of editor UI); the phone is the instrument. **Two
  traps:** the MCP `build` tool's `options: "Development"` was silently ignored (Build type
  'Release' in logcat, no `[perf]` lines); build through `BuildPipeline.BuildPlayer` in an
  eval with `--timeout 900` instead - it reports the 5 s main-thread timeout and builds
  anyway, watch the APK's mtime. And an EditMode run that stays at "running" forever with
  nothing in `Editor.log` (three times in a row, `cancel_tests` + reload did not help) came
  back on its own after the Player build; cause not established. Found in passing and
  fixed: `_levels` in the uncommitted scene had lost `Level_01` (Salih's hand edit), so a
  fresh machine would have booted `Level_02`; written back as `[01, 02, 03]`.
- **Performance pass, step 2: settings** - done, 80/80 green, both levels at a locked
  60 fps on the phone, idle and firing (`Level_01` was 33.3 ms, `Level_03` 36-38).
  Everything is in `Docs/PERFORMANCE.md` (2a-2f, tables per sweep, the summary table of
  what shipped); the short form: `targetFrameRate` 120, light shadows hard (Salih's
  call), post processing deleted outright (Volume object + `Post_Profile.asset`; Salih:
  "komple silebilirsin"), HDR off, `CustomShader` made Opaque with its two `clip`s
  removed (TCP2 generated it as TransparentCutout; no texture in the game has alpha, and
  a fragment clip kills early-Z on Mali: 7 ms on the 900-cube level), splash particles
  out of the shadow map, shooter Animators Cull Completely. Measured and NOT taken, with
  numbers: GPU instancing / draw count (390 fewer draws = 0 ms; the friend's case's
  headline tactic), shadow map 512, cubes not receiving shadows (0 once hard), render
  scale (2.5 ms, softens), Swappy (+2 ms, no 90 Hz). Instrument: `PerfSweep`
  (Presentation, dev-only, `_run` off in the scene; rewrite its variant list per
  experiment). **Traps:** (1) `PerfSweep` disabling itself in `Awake` still got
  `OnDisable`, and `Restore` wrote render scale 0 - a whole "final" measurement was
  taken on a blurry phone before the screenshots gave it away; now armed only after the
  baseline read. Check a screenshot before believing a number. (2) `shader_feature`
  keywords toggled at runtime do nothing in a build unless some material carried them at
  build time (`Cube_Hidden` did, temporarily, for the receive-off variant). (3) The
  EditMode runner hangs at "running" after a `recompile`; a Player build clears it every
  time (three for three). (4) Samsung's 60/90 Hz mode is a user setting ("Motion
  smoothness"); neither `targetFrameRate` nor Swappy overrides it, so true frame times
  under 16.7 ms are invisible until Salih sets Adaptive. Still open: 16 B/frame idle,
  ~49 B/frame while firing on `Level_03`; source not identified (not LeanTouch, not the
  EventSystem).
- **Performance pass, step 3: allocations** - done, 0 B/frame idle and firing in the
  editor's PlayerLoop (phone confirmation in `Docs/PERFORMANCE.md` step 3). Instrument:
  an eval that records 120 profiler frames with `ProfilerDriver`, walks the raw frame
  data for `GC.Alloc` samples, keeps only chains under `PlayerLoop` (editor UI garbage
  filtered out) and, with `memoryRecordMode = GCAlloc`, resolves each one's call stack -
  the script lives only in the session scratchpad, the pattern is in the ledger. Two
  sources, both closed: (1) the 16 B/frame idle was Easy Save's `ES3GlobalManager`
  coroutine doing `yield return new WaitForEndOfFrame()` every frame for a Cache
  location we do not use - the instruction is now a static, one line in the plugin
  (re-apply on an ES3 update). (2) Firing: `ShooterView.FaceForward` -> DOTween's
  `DORotateQuaternion` shortcut, three closures per call, called on EVERY targetless tick;
  `TurnTo` (`DOLookAt`) had the same shape per shot. Now one yaw `Tweener` per view built
  once with `DOTween.To` + `SetAutoKill(false)`, re-targeted with
  `ChangeEndValue(..., snapStartValue: true).Restart()` (the `CameraShake` pattern);
  `TurnTo` computes the yaw from the flattened direction, `FaceForward` early-returns
  while already forward. Verified in Play: yaws swing 300-60 while firing, settle to 0
  when targetless, zero errors. Left alone with the reason recorded: `Leave`'s
  `Vector3[8]` per departing shooter (DOTween's path keeps the array reference).
- **Performance pass, step 2g: Salih's look review** - done, 80/80 green. Salih saw "no
  shadows and no particles". Checked with pictures, not argument (`Docs/PERFORMANCE.md`
  2g): the floors never showed shadows (Apps' `GameArea_Floor.mat` has a pure white TCP2
  `_SColor`, `Floor.mat` one equal to its base colour - a tall probe cube at strength 1
  cast onto cubes and shooters only, at the pre-pass commit exactly as now); the splash
  is there (phone screenshot mid-shot); the old profile held only a vignette, no bloom
  (the "bloom" in earlier notes was a memory of an older profile). What he missed was the
  soft cube-on-cube gradient. Meanwhile he had set the phone to Adaptive (90 Hz), and the
  sweep at 90 Hz showed **soft Low is free** (11.1 ms, the cap) while vignette+HDR costs
  2 ms and soft High 9 ms. **Shipped: light shadows Soft, per-light softShadowQuality
  Low** (the scene's `m_SoftShadowQuality: 1`), post processing still out. Final on the
  phone at 90 Hz: `Level_01` 90 fps locked idle and firing, `Level_03` 63-71 fps, 0 B/frame
  except the seconds a shooter leaves. **Rule from this:** when Salih reports a look
  regression, bisect with captures at the pre-change commit before touching anything -
  two of the three "regressions" predated the pass.
- **Performance pass, step 3b: the reused-tween pattern on cubes and bullets** - done,
  83/83 green (two allocation tests, stable over repeated runs), verified in Play. The HUD
  had exposed that "0 B while firing" was measured after the bursts; the honest number was
  ~2 KB per shot, 65% of it `FlowBoardColumn`'s `DOMove` per surviving cube (DOTween
  shortcuts allocate two closures per call). `CubeView` now owns one
  `TweenerCore<Vector3,Vector3,VectorOptions>` built on first use: `SlideTo` (OutBack,
  the flow) and `FlyTo` (linear, the pooled bullet) re-target it with the **typed**
  `ChangeEndValue` + `Restart`. **Two traps, both found by tests/Play, not by reading:**
  (1) `Tweener.ChangeEndValue(object, ...)` boxes the Vector3 - 40 B per call, the
  allocation test stayed red until the typed `TweenerCore` overload; the shooter's yaw
  tween had it too. (2) `ToUniTask()` waits for the tween's **kill**, and a reused
  `SetAutoKill(false)` tween never kills: the flight await never resolved, no cube died,
  the bullet pool grew to 10 with every bullet stuck active. `AwaitForComplete` is the
  await for a reused tween; both director awaits (flight, slide) use it now. Bullet
  prewarm 5 -> 10 (flight 0.17 s is longer than the 0.12 s interval, so more than five
  are in the air). Left with reasons in the ledger: death `DOScale`, the per-await
  cancellation node, the counter punch, `Leave`'s path (~15% of the old garbage).
- **Performance pass, step 3c: `CollapseTweens`** - done, 86/86 green, verified in Play
  (`Level_03`, five seated: 30 deaths went through 6 pooled entries, every entry complete
  with a destroyed target afterwards, lock/settling counters all 0, verdict Lost by the
  rule, zero errors). Closing the per-shot garbage the ledger left: the dying cube's
  `DOScale` was two closures per death. A reused tween PER CUBE saves nothing (a cube dies
  once, the closures would be built at the death anyway), so the pool is shared: each entry
  owns one `DOTween.To` whose closures read an `Entry.Target` field, and a death re-points
  the field, `ChangeValues` + `Restart`. Grow-only, "not playing" is the free signal (the
  `ComponentPool` shape), `KillAll` from the director's `OnDestroy` because nothing
  auto-kills. Awaited with `AwaitForComplete` (the reused-tween trap from 3b). Probe:
  always-`Add` turned exactly the allocation test red. **Found in passing, NOT fixed:** the
  uncommitted scene's `_levels` holds only `Level_03` (Salih's hand edit for the perf
  screenshots); `ES3` index is -1 on this machine, so Play boots the stress level. Restore
  `[Level_01, Level_02, Level_03]` before the case ships.
- **Performance pass, step 3d: the counter punch reused** - done, 88/88 green, verified in
  Play (`Level_03`, five seated, a per-frame logger over every counter: peak scale exactly
  1.300, every counter back at 1.000 at rest, zero errors). `ShooterView.SetAmmo`'s
  `DOPunchScale` per shot (segment arrays per call) became one punch per view, built on the
  first tick with `SetAutoKill(false)` + `Pause`, `Restart`ed after (the `CameraShake`
  pattern); `Restart` snaps the counter to the rest scale first, which replaces the old
  `DOKill(complete: true)` drift guard. The punch is its own public `PunchCounter()`,
  called by `SetAmmo`, **because of a measured trap:** TMP's `SetText(string, float)` is
  allocation-free in a Player but ends with `#if UNITY_EDITOR m_text =
  InternalTextBackingArrayToString()` - a string per call in the Editor - so an EditMode
  `AllocatingGCMemory` assert on `SetAmmo` can never pass; `ShooterViewTests` measures the
  punch through `PunchCounter` and pins the rest scale through `SetAmmo`. Probe: building the
  punch on every call turned exactly the allocation test red. **Runner trap, hit three times
  here:** `recompile` reported `completed` while the test assembly stayed stale (new tests
  simply did not appear in the run); the unwedge eval from `unity-mcp.md`
  (`UnlockReloadAssemblies` + `Refresh(ForceUpdate)` + `RequestScriptCompilation`) fixed it
  every time. Left: `Leave`'s path array (3e).
- **Level editor: Save reaches Unity without a focus change** - done. Salih edited
  `Level_06` in the page, pressed Save, and Play ran the old level: the file on disk was new
  (three PUTs, 204 each) but Unity's imported `TextAsset` was still the old 1358 bytes - the
  editor-does-not-reimport-until-focused trap (CLAUDE.md line 186), and a designer working
  in the desktop app's pane never gives Unity focus. `serve.py` now runs
  `unity cmd eval AssetDatabase.ImportAsset(<file>, ForceUpdate)` in a daemon thread after
  every PUT, best effort (no CLI on the PATH, no editor, or a 30 s timeout all just fall
  back to Unity's own next refresh) and logs `unity: <asset> reimported`. Proven: a PUT of a
  temporary `Level_98.json` with no manual refresh left Unity holding 1083 bytes = the disk
  file. Also on this day: the five-colour rule was scoped to the sample level (commit
  `cae11b9`) - `LevelFileTests` applies it to `Level_01` only and checks ammo against the
  colours actually on each board; the editor's `E_MISSING_COLOUR` became `W_MISSING_COLOUR`. The hidden-shooter rule followed the same day at Salih's
  call: scoped to `Level_01` in `LevelFileTests`, `E_NO_HIDDEN` -> `W_NO_HIDDEN` in the
  editor; autofill still hides one by default, the designer may untick it. Proven with a
  two-colour, nothing-hidden temporary level in the folder: suite 84/84.
- **Level editor: on-screen strings in Turkish** - done, node 41/41, verified in the pane
  (labels, buttons, tooltips, statuses, the hint and every `validate` message; no console
  errors; `<html lang="tr">`). Salih's call, and the one exception to the English-only rule,
  now written into `.claude/rules/code-standard.md`: the editor's users are Turkish
  designers, so what they read is Turkish; domain words stay (shooter, slot, hidden, dock,
  level); code, comments, tests and commit messages stay English. `COLOUR_NAMES` are
  Sarı / Kırmızı / Mavi / Yeşil / Turuncu with the file letters unchanged; the tests match
  the Turkish fragments (`/sütunu 3/`, `/Turuncu/`, `/2 küp/`). A designer-facing string
  belongs in the page or in `validate`'s messages and nowhere else - the engine and its
  parser never learned a Turkish word.
- **Unity does not auto-refresh in this session, even when focused** - measured 2026-09-04.
  Salih saved `Level_06` from the editor; the file on disk changed (1055 bytes, his queue)
  and Unity kept playing the old import (1083 bytes). Auto Refresh is Enabled
  (`kAutoRefreshMode=1`; the legacy `kAutoRefresh` was False and is now True), yet a temp
  `Level_98.json` dropped on disk stayed invisible to `AssetDatabase` **after the Editor was
  given focus** (`isApplicationActive=True`). Most likely the `-automated` flag the Hub
  passes for the pipeline: Unity treats the session as unattended and skips focus-driven
  refresh. Not proven (that needs a relaunch without the flag); the rule that follows is
  proven: **after saving a level, press Ctrl+R in Unity (Assets > Refresh) before Play**,
  or run `AssetDatabase.Refresh(ForceUpdate)` through the CLI. A reviewer launching Unity
  normally gets the standard focus refresh. The deleted `serve.py` hook had masked this.
- **Level editor: layers as a count** - done, node 41/41. Salih's observation: every cube
  on a cell shares its colour (the rule `Level_02`/`03` were generated under and the one
  the game plays best), so a layer needs no grid of its own - it is a number. `level.layers`
  replaces the old multi-layer banner: `parse` keeps the count and reports `uniform` (every
  upper layer equal to the ground layer), `serialize` writes the ground rows once per layer
  (**`Level_02` and `Level_03` now round-trip byte for byte**, pinned), `stats` counts cubes
  x layers, `autofill(rows, columns, layers)` deals for every layer, `simulate` erodes a
  cell's stack one shot at a time before the row behind becomes the front (the generator's
  `living` counter), `validate` adds `E_LAYERS` (< 1) and `W_LAYERS` (> 1: the sample level
  is single-layer). The page has a Layers input next to Width/Height; Save is refused only
  for a file whose upper layers differ from its ground layer (`sourceUniform`), which none
  of the shipped files do. Verified in the pane: layers 3 on a fresh board reads 300 red
  cubes and raises `W_LAYERS`.
- **Level editor: `serve.py` deleted again, the page is local-only** - done, node 35/35.
  Salih's call, and the right one: the reviewers open the repo and double-click; nobody
  installs Python for a case. What survives is what a page can do by itself: the **File
  System Access API** in Edge/Chrome (pick `Assets/00_GAME/Levels` once, then Open/Save
  write in place; the handle is kept in IndexedDB so the next session offers "Reconnect")
  and, everywhere else or before a folder is picked, **Open reads a chosen file and
  Download hands the JSON back** to drop into the folder. The Unity reimport hook went with
  the server; the reviewer is in Unity anyway, and it refreshes on focus. **Proven in real
  Edge from `file://` with Windows automation**, not the tool's pane (which suppresses every
  native dialog): double-click, Choose Levels folder -> the native picker opens -> type the
  path, Select Folder -> Edge's bubble *"Allow this site to edit files? file:/// will be
  able to edit files in Levels"* -> Allow -> the button reads `"Levels" ✓`, the file list
  appears, the name field jumps to `Level_07.json`. **The trap that ate an attempt:** that
  bubble is dismissed the instant focus leaves Edge (the first run lost it to the Claude
  window and returned a silent `AbortError`), so the picker step must be done in one
  uninterrupted go. The status line now also goes into `document.title` ("Saved X — Blast
  Level Editor"), which is how a native-window automation reads it, and a designer sees the
  state in the tab. Two entries below this one describe the deleted server; they stay as
  the record of why it was tried and why it went.
- **Level editor, `serve.py`: the folder wired in by default** - done, Unity suite 84/84
  with `Level_06.json` (saved from the page itself) in the folder, and `Level_06` played to
  **Won** in the Editor (100 cubes, 9 shooters, 100 -> 24 -> 2 -> 0). Salih could not test
  the folder picker: the desktop app's browser pane **suppresses every native dialog** -
  `confirm()` returns false and `showDirectoryPicker` never opens (the console says so:
  "Page dialog suppressed"). That is also why Autofill "did nothing" for him: its confirm
  was auto-answered no. And he wanted a default path, which no browser page can have - the
  File System Access API exists precisely so a page cannot open an arbitrary folder. Both
  answered by the plan's escape hatch: **`python level-editor/serve.py`** serves the page
  and three requests - `GET /api/levels` (the folder path and its files), `GET` /
  `PUT /api/levels/<name>.json` - against `<repo>/Assets/00_GAME/Levels`, the path
  `LevelFileTests` walks. No picker, no permission prompt, works in the pane. The page
  detects it at boot (`fetch("/api/levels")`) and falls through to the picker, then to a
  download, when it is absent; the header shows the folder path in server mode. The one
  guard worth a test is `safe_level_name` (one plain `.json` name, no `..`, no separators):
  `python -m unittest level-editor/serve_test.py`, four cases. **Verified in the pane:**
  Level_01 opened (20/20, its queue verbatim), New -> paint -> Autofill -> Save As wrote
  `Level_06.json` (LF, indent 2, byte-identical after a round trip). Autofill lost its
  confirm and reports "replaced N shooters" instead; when every dealt shooter would be a
  front, the last chunk is split so one can be hidden. **Still `confirm()`-guarded, and so
  silently cancelled in the pane:** New/Open while dirty, shrinking a column count that
  holds shooters, Save As over another file - fine in Edge, where the dialogs show.
  `.claude/launch.json` (untracked) runs `serve.py` for the tool's preview pane.
- **HTML level editor** - done, `level-editor/` at the repo root (outside `Assets/`, so
  Unity never imports it), 34 node tests green, Unity suite 84/84 with the editor's own
  `Level_05.json` in the folder, and `Level_05` played to **Won** in the Editor (100 cubes,
  6 shooters, 100 -> 30 -> 0). Three files, no dependencies, no build, no server:
  `index.html` (the page), `logic.js` (pure functions: `parse`, `serialize`, `validate`,
  `stats`, `autofill`, `simulate`, `resizeBoard`, `paintCell`, `nextFreeName`),
  `logic.test.js` (`node --test level-editor/logic.test.js`; the fixtures are the real
  files in `Assets/00_GAME/Levels`, so the tests fail the day the two formats disagree).
  Salih's spec: portable for designers and PMs, adjustable grid / columns / shooters,
  guards that refuse a broken export, an autofill that deals shooters from the board's
  colour counts, JSON straight into the Levels folder with the right name.
  **What it encodes:** the page draws the level the way the player sees it - board on top
  with `rows[0]` at its bottom edge (`flex-direction: column-reverse`), slot strip, queue
  with depth 0 at the top of each column; a hidden shooter draws grey "?" but keeps its
  colour select visible. Every click is a call into `logic.js` and a full re-render (400
  cells is nothing); ammo inputs commit on `change`, or the re-render steals focus per
  keystroke. **Validation mirrors the game exactly:** the nine parser refusals and the
  four `LevelFileTests` rules are ERRORS that disable Save/Save As, so nothing the editor
  writes can turn the suite red; five WARNINGS are advice (not 10x10, slots != 5, a shooter
  colour with no cube, more shots than cubes - the shooter keeps its slot to the end -,
  hidden at the front). **Autofill:** per colour, cubes in chunks of 20 with the remainder
  last, so total ammo == cubes and no shooter is ever stranded; colours the front rows need
  first are dealt first, round-robin across columns, which puts them at the fronts in one
  modulo; column 1 position 2 is hidden. **Simulation:** the generator's greedy player
  (`Docs/Tools/generate_level_02.py`, `Sim` + `play(smart=True)`) ported with the random
  firing order made deterministic, run on every change of a legal level, a loss reported
  as a warning. **Serialisation** is `JSON.stringify(doc, null, 2)` + LF: byte-identical to
  the Python generators' `indent=2` (a test pins `serialize(parse(Level_04)) === Level_04`);
  `Level_01` would re-flow on its first editor save - do not re-save it before the case
  ships. **Folder access** is the File System Access API: pick `Assets/00_GAME/Levels`
  once, the handle is kept in IndexedDB so the next session offers "Reconnect"; Save is
  enabled only for a legal single-layer level with an open file, a multi-layer file (02, 03)
  opens with a banner and can only be Saved As under a new name; browsers without the API
  fall back to a file input and a download. `logic.js` is a classic script with a CommonJS
  guard because **Chromium refuses `<script type=module>` from `file://`**, and the page is
  opened by double-click. **Verified by automation:** paint, autofill, stats, checks, button
  states, no console errors (via a `python -m http.server` preview; the file:// path renders
  as a data: snapshot in the tool's pane and cannot load the sibling script - the real
  double-click works, the pane does not). `Level_05.json` was produced through the same
  `autofill -> validate -> serialize` path under node, force-imported, and won in Play.
  **Not automatable, Salih tests by hand once:** the native folder picker - double-click
  `index.html` in Edge, Choose Levels folder -> `Assets/00_GAME/Levels`, open `Level_01`
  (20/20 per colour, zero errors), open `Level_02` (banner, Save off), New -> paint ->
  Autofill -> Save As `Level_06.json`, then switch to Unity (it reimports on focus) and Play
  it. **Runner trap, hit hard here:** the EditMode runner stayed at "running" through four
  attempts (cancel, the unwedge eval, `RequestScriptReload`), starting right after a
  `run_tests` was issued while an eval had just force-imported a level; the planned
  negative probe (under-ammo file -> `LevelFileTests` red) was dropped on that account - the
  same assertion was shown red earlier the same day on the ten-column `Level_03`. A Player
  build is the one fix the ledger has seen work every time. Skipped on purpose: undo, layer
  UI, drag-reorder, random-interleaving robustness; `.claude/launch.json` (the preview
  server config) is left untracked.
- `Level_04`, the mid-size benchmark - done, 84/84 green. The ledger had 100 cubes
  (`Level_01`), then jumped to 600 and 900, with nothing in between;
  `Docs/Tools/generate_level_04.py` fills the gap with **400 cubes and 20 shooters** - a
  10x40 board and 5 queue columns x 4 at 20 ammo, one colour per column, winnable by
  construction. Measured in the editor at a 512x1024 game view with vsync off and a 8.33 ms
  target, so nothing was capped: **11.0-11.3 ms (89-92 fps), 376 batches, 183 shadow
  casters, 137.8k tris, and 10 SetPass calls.** The SetPass number is the one worth
  keeping - 376 batches collapsing into 10 state changes is the SRP Batcher doing its job,
  the same check `Docs/PERFORMANCE.md` step 1 recorded as "SetPass 11-14 so the SRP Batcher
  is fine". It holds at four times the shipped level's cube count.
- `Level_03` cut from ten queue columns to five - done, 84/84 green, verified in Play
  (booted `Level_03`: 100 shooters spanning x -4..4 against a camera half-width of 5.45,
  **zero off-screen**, columns at x -4/-2/0/2/4, 900 cubes). Salih spotted it on the
  screenshots: the level declared 10 shooter columns and the queue is centred on x = 0 at
  2 units apart - the same spacing as the five slots - so it spanned x -9..9 and four whole
  columns sat outside the frame where nobody could tap them. Depth off-screen is fine and
  intended (the brief wants exactly two rows visible behind the selectable one); width is
  not. `Docs/Tools/generate_level_03.py` is now `QCOLS, QDEPTH = 5, 20`: still 100 shooters,
  still 9 ammo each, still 900 = 900, and the winnability argument survives unchanged
  because each colour now owns exactly one column instead of two, so its front is always
  that colour. `LevelFileTests` gained `MaxQueueColumns = 5` inside the existing per-file
  loop - red first against the old file with the right message ("10 shooter columns, and
  only 5 fit across the dock"). The brief leaves the column count to the level (Not3); what
  caps it is the dock's width, and that is what the constant's comment says.
- **Performance pass, step 3e: the burst re-measured** - done, ledger updated
  (`Docs/PERFORMANCE.md` 3e). Salih saw `gc 28506 B` on the HUD and asked what happened to
  "0 B". Two answers, both recorded there: the HUD reads `GC Allocated In Frame`, a
  process-wide counter that in the editor is ~28 KB/frame of Unity's own UI, and my chat
  summary of "0 B" was broader than the ledger ever claimed (idle 0, burst not 0).
  Re-measured with allocation callstacks on a window where **25 cubes actually died**:
  the game's own share is **3570 B over 300 frames = 11 B/frame**, against 13712 B of
  `Debug.Log` machinery in the same window. Biggest game entry is the per-await
  `CancellationToken.Register` (1104 B); nothing in the list is worth another pass.
  **Two traps worth more than the numbers.** (1) The first two runs of this measurement
  reported a clean `0 B` while **nothing was firing at all** - the shooters were seated but
  the level had no matching target. An allocation measurement must prove the work happened;
  the cube count is part of the record now. (2) A compile triggered while Play is running
  (an eval with a syntax error will do it) can make Unity's exit-Play scene restore drop
  serialized references. `GameDirector` lost `_pools`, `_audio` and `_shake` this way while
  the scene file on disk stayed correct, and the in-memory scene was left **dirty**. It
  reads as a gameplay bug - shooters fire, ammo drains to zero, no cube ever dies - because
  `ShotVisual` throws a `NullReferenceException` on its first line and `FireLoop` does not
  await it, so the loop keeps spending ammo. **Do not save the scene; reopen it from disk**,
  and check `scene.isDirty` after any eval-driven probing.
- Muzzle splash and bullet moved out in front of the shooter - done, 84/84 green,
  measured in Play (six sampled splashes, flat distance to the nearest seated shooter
  0.60 = `MuzzleReach`; it was 0.00 before, the splash sitting on the shooter's own
  origin). Salih's report, repeated three times before it was heard: splashes were
  spawning "inside the cube". The cube he meant was the SHOOTER, not the target. Two
  splashes exist per shot and only the impact one had an offset (`ImpactPullback`);
  the muzzle one had `MuzzleHeight` alone, which lifts but never reaches forward, so
  the shot left from inside the body that fired it. `Firing.MuzzleReach` (0.6) now
  offsets the spawn point along `aim.normalized`, and the bullet leaves from the same
  point - the local is named `chest` before the reach and `muzzle` after, so the two
  cannot be confused again. **The lesson, and it cost hours:** when a report says a
  visual is in the wrong place, read the reported Transform values first and find which
  object that coordinate belongs to. The pile of splashes at z=-10 was explained as
  "by design, that is the muzzle" three times while -10 was in fact the shooter's own
  centre, which is not where a muzzle is.
- Splash lifetime cut to the fire rhythm - done, 84/84 green. Salih's report: with the
  pullback set high, one splash flew away as asked but "ten more" stayed piled at the
  front, and he read that as a bug in the offset. It was not. Measured, seating every
  front on `Level_01` and bucketing the active splashes by z: **12 sat at z = -10 exactly
  (the slot row) and 8 at z < -11 (pulled back)** - the pile is the MUZZLE splash, a
  separate spawn at the shooter that the pullback does not govern and should not.
  Two things were cleared on the way, both by measurement, both worth not re-investigating:
  the pool does **not** leak (0 of 48 active in four samples once firing stops, and it
  never grew past its prewarm), and there is no third splash source (two `Splashes.Take`
  calls in the whole project, zero `SplashEffect` objects in the scene outside the pool).
  The real defect the pile exposed: a shot happens every 0.12 s and every splash lived
  **1.7 s** (`duration` 1 + particle lifetime 0.7), so ~14 copies stacked at one fixed
  world point per muzzle - a constant white glow instead of a per-shot puff. Fixed on
  `AppsAssets/Prefabs/SplashEffect.prefab` (Apps' asset, tuned not replaced): root
  `duration` 1 -> 0.25 and lifetime 0.3-0.7 -> 0.15-0.35 (total **0.6 s**), `InnerSplash`
  1 -> 0.25 and 0.25-0.35 -> 0.12-0.2. Stacking is now `0.6 / 0.12 = 5`. Verified live by
  reading a pooled clone's own `main` (total 0.6 s), and the same-phase sample fell from
  12-13 to 4. **Measurement note:** counting active splashes is phase-dependent - firing
  on `Level_01` comes in one- to two-second bursts, and a CLI round trip is ~1 s, so a
  snapshot lands wherever it lands. Read the clone's lifetime and do the arithmetic;
  do not trust a single active-count sample. Salih's own tuning of `MuzzleHeight` and
  `ImpactPullback` in the scene is his, left alone.
- Impact splash pulled off the cube's centre - done, 84/84 green, verified in Play (fifteen
  shooters seated on `Level_01`, board splashes sampled mid-fire: every one sat 0.30-0.34
  on the shooter side of the front row it hit, zero errors). Salih's report: the impact
  splash looked buried inside the cube. It was spawning at `cube.transform.position`, the
  cube's exact centre, so half the effect rendered inside a 0.9-wide cube and read as a dim
  flicker. `Firing.ImpactPullback` (0.45, half a cube) now moves it back along
  `aim.normalized` - **back, not forward**: the splash's own forward IS the flight
  direction, so offsetting along it would push the effect deeper in. Negative values do
  that on purpose, which is what the tooltip says. The muzzle splash is untouched.
  No test: one line of arithmetic on a humble visual, no branch, and pinning the number
  would be pinning a choice.
  **Correction to the trap this file repeats:** "a new field inside an existing serialized
  struct arrives as 0" is only true when the struct FIELD has no initializer. `GameDirector`
  declares `[SerializeField] Firing _firing = Firing.Defaults;`, so Unity deserialises the
  scene's data over an already-constructed `Defaults` and a sub-field missing from the scene
  keeps its default - `ImpactPullback` read 0.45 before anything was written. It was still
  written through `SerializedObject` so the scene states it outright. Check the declaration
  before assuming the eval is needed; the older chunks that hit the trap
  (`ShooterMotion`, `CubeDeath`) are the ones whose fields had no initializer.
- Salih's early playtest notes (unwired animator, missing dock visual, cramped framing)
  and the "next: overlay, then juice" line that followed them are **deleted as of
  2026-09-04: every item in them shipped** - the animator in "Shooter animator wired",
  the dock in "Framing matched to the original", the framing in that same chunk, and the
  whole overlay/juice list in the chunks between. They were quoted as outstanding work in
  a later session before anyone checked them against the entries above. Read the Status
  entries, not a parked to-do list; when the two disagree the entries are the record.
- Review pass before delivery (2026-09-05): graphify installed (`uv tool install
  graphifyy`, output kept out of `Assets/`, in the session scratchpad) and the whole
  suite read file by file. Verdict in short: layering real and test-guarded, Domain and
  Application clean, views humble; the three visible weaknesses were **JuiceConfig
  registered and never read**, **VContainer built and never resolved from** (every
  dependency was handed over by a manual `Construct` call in `Configure`, not one
  `[Inject]`, `Resolve` or entry point in the repo), and **GameDirector untested** (left
  as is: splitting the orchestrator two days out is a bug-free risk the case ranks above
  architecture; say so in the README instead). Also noted for the README: reveal of a
  hidden shooter fires when the step-up starts, not when it lands; comment density is
  38% of production lines; `Protected Methods` is not one of the standard's regions.
- JuiceConfig deleted - done, 83/83 green. Script, `Juice.asset`, `JuiceConfigTests`,
  the scope field and property, and the scene's `_juice` line are gone. The scope test now
  checks `Level` instead: an empty level field is the same silent null.
- VContainer does the wiring - done, 84/84 green (the `[Inject]` guard replaces the
  deleted juice test), verified in Play through the container itself: `Resolve(GameLoop)`
  is the very instance `GameDirector._loop` holds, 100 cubes and 15 shooters spawned,
  selecting column 0 fired all ten shots (domain 90 standing, 90 views, slot 0 freed at
  ammo 0), `LevelEndViewModel.Restart.Execute` reloaded the scene into a fresh scope (one
  scope, 100 cubes, Playing), zero console errors. `Configure` now registers instead of
  constructing: the three parsed models as instances, `IColorMaterials ->
  PaletteColorMaterials`, `GameLoop` and `LevelEndViewModel` as singletons, the three
  scene components through `RegisterComponent` (its build callback resolves the component,
  which is what runs the `[Inject] Construct` - confirmed in VContainer 1.19's source,
  the comment there reads "Force inject execution"), and the restart as an entry point:
  `SceneRestarter : IInitializable, IDisposable` subscribes to `Restart` and reloads the
  active scene, disposed with the container. `Blast.Presentation` and `Blast.UI` reference
  `VContainer` for the attribute only; `ArchitectureTests` ignores non-Blast references,
  so the layer rule is untouched. **Rejected:** `autoInjectGameObjects` (inspector list,
  invisible in a diff) and a `RegisterBuildCallback` lambda for the restart (works, shows
  nothing). New test `GameScene_EveryRegisteredComponentHasAnInjectMethod`: a scene
  component registered without the attribute builds fine and throws on the first tap, so
  the mistake needs a test, and it was red before the attributes went in. **Trap:** in an
  `eval`, `scope.Container.Resolve<T>()` does not compile - the generic overload is an
  extension method in the `VContainer` namespace and eval drops usings; cast the
  non-generic `Resolve(typeof(T))` instead.
- Hidden shooter reveals on arrival - done, 86/86 green, verified in Play (column 4 on
  `Level_01`: the hidden G at depth 1 still wore `Cube_Hidden` with one material slot in
  the same frame the tap was processed, and `Cube_Green` with the outline slot and counter
  5 two seconds later; zero errors). Slide 7 says the colour shows when the shooter
  reaches the front row; `StepQueueForward` had been revealing it the instant the step
  began. The reveal now rides on the front view's step tween (`OnComplete` ->
  `RevealFront`, one closure per tap, the budget `SetOutlined` already spends), and
  `PopFrontShooter` completes that tween WITH callbacks first, so a shooter tapped
  mid-step leaves revealed and the outline comes off after the reveal put it on - the
  case a fast player hits, guarded by `PoppingAShooterMidStep_RevealsItAndKeepsTheOutlineOffIt`.
  **Rejected:** returning the tween for the director to await (the mid-step pop needs the
  same completion trick anyway, and the spawner test could no longer see the reveal).
  **Trap:** a shooter's renderers are on children - in an eval read `_coloredParts[0]`,
  `GetComponent<Renderer>()` on the clone root throws.
- Shot pitch jitter - done, 87/87 green. `ShotAudio.Play` sets the voice's pitch to
  1 +/- `_pitchJitter` (0.1, inspector `Range`) before `Play`, so five slots firing the one
  sample eight times a second read as many shots instead of a buzzer. `Random.Range` is
  allocation-free. `ShotAudioTests` (red first: SetUp could not find the field) checks
  24 shots stay inside the band and do not all land on 1.
- Hit swell on the dying cube - done, 88/88 green, verified in Play (two shooters, 20
  kills, domain 80 / views 80, zero errors; voice pitches read 0.961 and 0.900 mid-burst).
  `CollapseTweens.Play(target, duration, swell)` eases with `InBack` and the swell as its
  overshoot: one tween is both the flinch (the overshoot pulls the scale past its start)
  and the death, so nothing new is allocated per kill and the reuse tests still hold.
  `CubeDeath.HitSwell` (2.5, written into the scene too) is the knob; 0 is the old plain
  shrink. `ACollapseWithSwell_GrowsBeforeItShrinks` reads the scale at 40% through `Goto`
  and expects it above the start with swell and below without. **Trap:** `Tween.Goto`
  pauses the tween by default, and the pool reads a paused tween as idle and hands it to
  the next cube - pass `andPlay: true` in a test that samples mid-tween. **Not
  established:** a single mid-burst scale sample did not catch a cube over its rest size;
  the rest size is the prefab's, not 1, so the probe's baseline was wrong. The unit test
  is the proof; a slow-motion look in the Editor is the way to tune the number.
- Regions and README - done. `GameLifetimeScope`'s `Protected Methods` region (not one
  of the standard's names) is `Public Methods`; `GameDirector`'s `Awake` / `OnDestroy`
  moved from the Public region to Private, where every other file keeps its Unity
  messages. `README.md` written for the evaluator: run, tests, the six assemblies and
  what each knows, the level format, the performance summary, and the decisions list -
  including, in the open, that `GameDirector` has no unit tests and why the split was
  deferred.
- Diagnostics assembly - done, 88/88 green. `PerfProbe` and `PerfSweep` moved to
  `Scripts/Diagnostics/` under `Blast.Diagnostics` (namespace and asmdef), which
  references TMP and URP only and no game layer; the URP reference left `Blast.Presentation`
  with them. `git mv` kept the script GUIDs, so the scene's two components still resolve.
  `ArchitectureTests` gained the row (red first: the table named an asmdef that did not
  exist). Rationale: the two dev-only probes were compiling into the gameplay assembly
  and pulling URP into it; a profiler tool is not Presentation.
- Hit swell REVERTED (2026-09-05) - 87/87 green. Salih could not see it, and the log
  already said why: this death had been an `InBack` pop once and a jelly swell to 1.33
  once, and both went when the original was measured frame by frame ("what it does NOT
  do: no jelly swell"). At 0.18 s the swell is 5 frames under the impact splash and the
  shake - the same threshold the hop failed. `CollapseTweens.Play` is back to the two-
  argument `OutQuad` shrink, `CubeDeath.HitSwell` and its test are gone, and Salih's
  02:41 experiment in the scene (`CollapseDuration` 0.27, `HitSwell` 1.5) was discarded
  with it - the measured 0.18 stands. **Process failure, mine:** the change was made from
  the `GameDirector` comment alone without grepping this log for the component, which is
  the first line of CLAUDE.md's rule. Grep the log for `swell`, `hop`, `rock` before
  proposing any cube-death juice again; the answer is already here. The pitch jitter
  stays.
- Cube death shaped by an AnimationCurve - done, 89/89 green, verified in Play (default
  curve arrived from the struct initializer with 2 keys, y(0)=1, y(0.5)=0.25, y(1)=0; the
  first cube traced 0.76 -> 0.01 over 13 frames; ten kills, domain 90 / views 90, zero
  errors). Salih's call after the swell experiments: none of the ease presets was what he
  wanted, he wants to draw it. `CubeDeath.CollapseCurve`: x = fraction of
  `CollapseDuration`, y = scale as a multiple of the scale at impact (1 at start, 0 at the
  end, above 1 for a flinch), editable in Play. `CollapseTweens.Play(target, duration,
  curve)` stores the reference on the entry and the ease built once in `Add` is a custom
  `EaseFunction` reading `1 - entry.Curve.Evaluate(t)`, so a death still allocates nothing
  (`ACollapseWithTheSameCurve_DoesNotAllocate`) and an inspector edit is picked up on the
  next death. Default curve = (1 - t)^2, the measured OutQuad shrink, keyed with tangents
  (0, -2) and (0, 0); written into the scene as well. **Rejected:** `SetEase(AnimationCurve)`
  per Play (DOTween wraps the curve in a new EaseCurve object per call - the allocation
  the pool exists to avoid). Also closes: `ImpactPullback` had been 5 in the scene since
  `aaedd05`, putting the impact splash on the shooter's side of the room; back to 0.45
  (`cb23da6`), which is why "no particle at the cube" was true.
- Shots in the shooter's colour - done (2026-09-05), 92/92 green, verified in Play (a blue
  shooter: blue bullet, blue trail, blue muzzle splash, blue impact splash; five active
  splashes all reporting `startColor` = the palette's blue; no console errors). Salih
  asked whether the death particle could be coloured without allocating; it could, and
  the bullet and its trail came with it so one shot reads as one colour. Five commits:
  scene curve tuning pushed first so it did not sit orphaned; `IColorMaterials.TintOf`
  (forwards to `PaletteData.ColorOf`, the `Tint` field the palette already carried "for
  particles"; untested like the adapter's `MaterialOf`, no branch); `CubeView.Tint` sets
  the trail `SpriteRenderer.color` (per-renderer vertex colour on URP's
  `Sprite-Unlit-Default`, `Gun_Sprite.png` is white + alpha) and `Bullet.prefab` wires
  the `Trail` child; `SplashView` (`_systems[]`, `Tint` sets `main.startColor` on the
  burst and `InnerSplash` - both render through URP `Particles/Unlit`, which multiplies
  vertex colour in; `ASecondTint_DoesNotAllocate` green) on `Splash.prefab`, a variant of
  Apps' `SplashEffect` the way `Bullet.prefab` wraps their model; `ShotPools` pools
  `SplashView` and the scene points at the variant (written through `SerializedObject`,
  saved, `isDirty` false); `GameDirector.FireLoop` resolves `SlotRow.ShooterAt(slot).Color`
  once per seating into material + tint and `ShotVisual` applies `Wear` on the bullet
  body (`sharedMaterial`, same TCP2 shader as the cubes, so the SRP batch is unchanged),
  `Tint` on trail and both splashes. **Skipped:** a `SlotRow.ColorAt` reader - `ShooterAt`
  already exists; a `GetComponentsInChildren` per play (allocates; the serialized array
  is why `SplashView` exists). **Trap:** a `Take` on a `ComponentPool<ParticleSystem>` had
  the scene pointing at the prefab's ParticleSystem component; retyping the pool leaves
  that reference null until it is re-pointed at the new component type. Editor probe
  numbers were read and ignored as always; the phone sweep is the ledger's job.
- Coloured shots measured on the phone - done (2026-09-05), `Docs/PERFORMANCE.md` 3h. Two
  Development APKs (`15305b5` before, `1aaf523` after) through the identical adb script:
  idle 0 B, cold burst ~3.8 KB/frame-seconds in both, warm burst 432 vs 465, 90 fps /
  11.11 ms and identical batch counts throughout; the difference is tap timing, the
  shape is 2g/3e's seats and departures. Phone screenshot mid-burst: red bullet, red
  trail, red splash next to a blue one. **Recipe notes:** the `before` build came from
  `git checkout <commit> -- Assets/00_GAME` in place, then `AssetDatabase.Refresh` +
  `RequestScriptCompilation` AND `EditorSceneManager.OpenScene` - the refresh recompiles
  but does not reload the open scene, so `_splashes.Prefab` read null (new component type
  in memory against the old field type) until the scene was reopened from disk; same
  again on the way back to `main`. `recompile` said `up_to_date` after the restore while
  the DLLs were in fact fresh - checked by mtime, as the rule says. The launcher activity
  is `UnityPlayerGameActivity`; `monkey -p com.APPS.CaseStudy 1` launches without
  knowing it. **Lesson:** `adb pull` the installed APK before overwriting it; the A/B
  cost a second build because the old one had been deleted "so the new file is proof".
- Impact splash on the cube's top face - done (2026-09-05), 92/92, verified in Play (paused
  at the frame the impact splash appeared: y 0.95 on a cube centred at 0.5, the red burst
  opening on the front-left cube's top). Salih: the splash was "staying inside the cubes".
  `Firing.ImpactLift` (default 0.5, half a cube; written into the scene) lifts the impact
  point along +Y; `ImpactPullback` still pulls it toward the shooter, so the two together
  put it on the near edge of the top face. The struct field's `= Defaults` initializer
  delivered 0.5 to the already-serialized scene struct without a hand write (checked:
  `arrivedAs=0.5`), the trap from the CLAUDE.md list not biting when the initializer is
  there. Tune in Play: a bigger lift floats it, a smaller pullback centres it on the face.
- Original game read, level converter - done (2026-09-06), 92/92 green, `Docs/ORIGINAL_GAME_ANALYSIS.md`.
  Salih wanted the APK extraction shown off; the honest form is the tool plus the numbers
  it produced, not the content. `extract_levels.py` / `verify_extracted_levels.py` moved
  from the root `ExtractionTools/` into `Docs/Tools/` (output `extracted_levels/` stays
  gitignored: 2447 levels, 6 A/B cohorts). New `convert_original_level.py`: their
  ScriptableObject (board row-major with 15 flags per cell, deck column-major with depth 0
  selectable, `SecretItem` = hidden, ammo NOT authored) -> our JSON, crop to 10 rows, palette
  remap, exact-fit ammo, then a DFS planner under GameRules (leftmost target, five slots,
  no merge) plus an adaptive replay under random firing order. **Findings:** no level in
  the 2447 is 10x10 with our five colours (widths are all 10, heights 11-20); the closest is
  Control Cohort 21 (10x12, 3 columns, 27 shooters, 18 hidden). Converted as
  `Level_07.json` (`--flip`: the file's last row read as the front, because the other
  reading takes a blind player from 55% to 9%). Oracle 100% in the sim, blind random 55%
  (hand-made `Level_01`: 100% / 60%). **Then the real game disagreed:** an in-game driver
  (eval hook calling `GameDirector.OnShooterSelected` on the selectable view, replanning
  from the live domain state before every tap) lost at tap 12 with no winning line left.
  Column locks and same-colour run-in races decide which shooter drains, and with exact-fit
  ammo that decides whether a slot frees; the original covers this with merge and surplus
  ammo, both forbidden here. **Decision:** `Level_01` stays hand-authored and boots; the
  converted level ships as `Level_07` (proof of the tool, hard extra level), and the doc says
  why. **Rejected:** shipping their level 21 as the sample (the tester loses on the
  original's balance, rubric 1); surplus ammo (a shooter with ammo and no colour left holds
  its slot, worse); reading the original's mesh or post-process (the mesh is Apps' asset
  by the brief, post-processing was removed on measurement in 2f/2g). **Trap, mine:**
  `cp` to `Level_05.json` overwrote Salih's editor-reworked level (`9714b0e`) for a minute,
  restored from git; look before naming a file into `Levels/`.
- Converted level taken whole - done (2026-09-06), 92/92 green. Salih: no `Level_07`, no
  crop; take a dense hard level of theirs as a test level, the game supports full boards.
  Only five clean levels use just our five colour indices and all are easy 10x12s, so colour
  renaming is unavoidable for a dense one; the pattern is what matters. Ranked the clean
  10x20s with at most five colours and five columns: Control Loop 299 (60 shooters) is
  fragile in both orientations (oracle 24% / 36%); **Control Cohort 18** (55 shooters, 14
  hidden, their `difficultyLevel` 2) is oracle 100% with row 0 as the front (2% flipped),
  blind random 6%. Shipped as `Level_Original_18.json`; `Level_07` removed. **In-game
  oracle drive won in 55 taps** (scene `_level` swapped through `SerializedObject` without
  saving, `drive.py` replanning from the live domain state before each tap, then the scene
  reopened from disk). Converter's five-colour/hidden gate dropped: those are `Level_01`'s
  rules and `LevelFileTests` owns them. Lesson recorded in the doc: crop a level and the
  exact-fit balance goes with it; take it whole or not at all.
- `Level_Original_04` - done (2026-09-06). Salih: "level 4 in the APK is very good, take it".
  Control Cohort 4 whole: 10x12, 120 cubes, indices 2/3/5 = our Y/B/R so no renaming, 26
  shooters in 5 columns, no hidden. Oracle 100% in both orientations (same column-drain
  line), blind random 93%; **in-game oracle drive won in 26 taps.** Sits next to
  `Level_Original_18`; doc, README and map updated. **Trap:** the EditMode runner stuck at
  "running" twice in a row (no recompile involved); `cancel_tests` + the unwedge eval did
  not clear it. Not chased: the level's file checks (ammo == cubes, five columns) hold by
  construction and the in-game run is the stronger verification. Salih runs the suite from
  the Test Runner window; if the hang persists, the Player build has cleared it every time.
- Board reading corrected, `Level_Original_18` withdrawn - done (2026-09-06). Salih compared
  our level 4 with the APK's: same deck, different board. The board is **column-major**
  like the deck (cell = column * height + row) and the file's **last row faces the
  shooters**; read row-major it was noise that happened to be solvable, and read the wrong
  way up level 18 was solvable while the real level 18 is not. Both facts checked against
  the running APK (front row `YYBBYYRRYY`, deck Y R Y B Y / R Y Y Y B). Also read off its
  HUD: ammo is `requiredCount` = 20 per shooter (520 = 26 x 20), not exact fit; the merge
  feature is what lets surplus ammo not clog the slots. Converter fixed; `Level_Original_04`
  regenerated (blind random 70%, in-game oracle won again, 26 taps, screenshot for the
  README). **Dense levels re-scanned with the corrected reading:** 18 and 650 unsolvable in
  the sim; 868 (10x20, 50 shooters, 18 hidden) oracle 100% in the sim and **lost the real
  game at tap 12** (three blue shooters seated 1/4/4). Same mechanism as the cropped 21:
  exact-fit ammo makes same-colour races decide slot frees. **Decision:** only level 4
  ships; 18 removed; 868 not added; no more dense candidates chased (968, 299 are the same
  class). **Trap:** `AssetDatabase.Refresh(ForceUpdate)` right after
  `ApplyModifiedPropertiesWithoutUndo` saved the scene to disk with the swapped `_level`;
  `git checkout -- Game_Scene.unity` + reopen fixed it. Swap, Play, then reopen from disk;
  never Refresh in between.
- Level editor: JSON kopyala / JSON yapıştır - done (2026-09-06), node 41/41 (UI glue over the
  tested `parse` / `serialize`, no new logic). Salih: the editor only wrote into one folder,
  which leaves Safari and Firefox (no File System Access API) with downloads only, and
  there was no way to hand a level to someone in a message. `copyJson` puts
  `Level.serialize(state.level)` on the clipboard; `pasteJson` opens a `<dialog>` with a
  textarea (prefilled from the clipboard where reading is allowed) and its Yükle runs the
  text through `loadText`, so a bad paste is refused with the parser's own message and the
  dirty check applies. Verified headlessly (Edge `--headless --dump-dom` on a copy that
  calls the dialog): status "JSON yüklendi", 120 cells for level 4. The Browser pane
  renders file:// pages as static snapshots (no scripts), so it cannot test this editor.
  Report and README now say where the editor is, why HTML, how it opens on Windows and
  macOS, and which browsers write to the folder (Chromium ones) versus copy/paste.
- Brushed cubes lean as a bullet passes (2026-09-07) - done, 103/103 (FlightPath 6,
  CubeView +3, LevelSpawner +2). Salih's request from a frame-by-frame read of a This is
  Blast capture (25 fps, 7.8-8.6 s): a bullet crossing a non-target cube on its way leans
  it 15-20 deg into the travel direction, peak at +40 ms, upright with a small
  counter-swing by ~160 ms; the neighbours of the hit cube take the same lean at impact.
  Three chunks, three commits: `FlightPath.EnterFraction` (slab test of the muzzle-target
  segment against a cube's XZ footprint, answered as a fraction of the flight);
  `CubeView.Nudge(signedAngle, duration, shape)` (one reused float-clock tween whose setter
  writes yaw absolutely, so overlapping brushes never stack and the cube ends upright);
  `GameDirector.BrushAlongFlight` polled every frame of the flight (`UniTask.Yield` loop
  replaces `AwaitForComplete` on the bullet; a bit per column remembers what this bullet
  brushed, no closure, no array). Settings: `Brush` struct (`Angle` 18, `Duration` 0.2,
  `Shape` curve peaking at 0.2) with `= Defaults`. Spawner gained `TryPeekFrontCube`,
  `BoardColumns`, `CellSize`. **Play verified** with an `EditorApplication.update` probe
  reading every board cube's yaw: one shot's brush read 7.7 -> 17.5 -> 15.5 -> 8.4 -> 0.9
  -> 2.7 -> 5.2 -> 5.0 -> 3.1 -> 0.8 deg over eleven frames, zero errors. **Geometry
  finding:** slots at z -10 and the board front at z -5.8 make every flight steep, so a
  bullet clips at most the target's neighbour near the end of its flight - the original's
  impact spread, not a long pass-through; a level with wider slot spacing would brush more.
  Rejected: colliders/triggers per cube (a hundred rigid bodies for a jiggle),
  `DOPunchRotation` per brush (segment arrays plus closures at ~40 brushes a second), an
  `OnUpdate` closure per flight (allocates). Not done: on layered boards only the stack's
  top cube leans. Note: the working tree had `Game_Scene.unity` on `Level_03` and a modified
  `Level_06.json` before this work (Salih's, uncommitted, untouched); the case's shipped
  scope still boots `Level_01` in git.
- Brush is a shove about the hit, not a spin about the centre (2026-09-07) - done, 106/106
  (CubeView 9). Salih on the first version: the cubes spun in place around their own centre
  with a yoyo ease, which reads as a top, not a hit - a punch on the shoulder swings you
  about the point it landed, pushes you back a little, and you come forward to recover.
  `CubeView.Nudge(arm, push, angle, duration, shape)` now: `arm` = centre to the contact
  point (the flight's entry into the footprint, from `FlightPath`), torque sign and lever
  from `arm x push` (a grazing hit spins the full angle, a centre-line hit only pushes),
  the centre moved by the swing about the contact plus `push`, all from one reused
  float-clock tween. `Brush.Push` (0.12) joined the settings. **Design change:** the slide
  and the shove compose - the move tween now drives `_rest` and both setters write
  `_rest + _offset`, so a slide starting mid-shove never adopts the shove as its start
  (`ASlideDuringANudge_StillEndsAtTheRest`); `SyncRest` reads the transform back before
  either tween restarts, which is what keeps pooled bullets (moved by the pool between
  flights) flying from where they were placed. **Play verified**, same yaw probe plus the
  offset from the spawn position: yaw 4.6 -> 13.3 -> 12.4 -> 8.7 -> 3.1 -> 0.5 -> 2.3 ->
  3.9 -> 3.7 -> 2.4 -> 0.9, shift 0.026 -> 0.067 -> ... -> 0.003, then the offset flips
  sign for the counter-swing (0.024 the other way) and returns; zero errors. Peak below
  the 18 deg setting because a corner clip's lever is under one. **Trap in the tests, twice:**
  a constant "full lean" curve never returns to zero, so a test that completes the tweens
  and expects the rest must use a shape that ends at 0; and a nudge with a zero push has
  no lever direction, so the swing test needs a push. The torque sign was written
  backwards first (`-angle * cross.y`); the side test caught it.
- The shove flees the hit (2026-09-07) - done, 107/107 (CubeView 10). Salih's sketch: the
  bullet lands on a corner, the cube turns (black arrow) and is pushed from the contact
  point out through its centre (yellow arrow) - away from the hit, not down the bullet's
  line. `Nudge(arm, travel, push, angle, duration, shape)`: `travel` is the bullet's
  direction and only feeds the torque; the push direction is `-arm` (a dead-centre hit has
  no side to flee and goes the bullet's way). The director passes `aim` and `Brush.Push`
  instead of a push vector. New test `Nudge_PushesAwayFromTheContactPoint` (angle 0, hit on
  the right, bullet forward: the cube goes left). **Play verified:** corner clip on column
  3's front (home -1.35, -5.78), offset peaked at (-0.234, +0.143) = away from the
  front-right corner, yaw 20.7 peak (the default curve's auto tangents overshoot 18 by a
  little; flatten the 0.2 key if it matters), shift 0.274 at the peak, counter-swing to
  0.092 the other way, at rest by frame 16 of the shove. Zero errors.
- A brush lands on the layer the bullet flies at (2026-09-07) - done, 109/109 (LevelSpawner
  +2). Salih on Level_03 (three layers): whichever cube of a stack the shooter hit, the
  neighbour's TOP cube shook. Cause: `TryPeekFrontCube` hands out the top of the front
  stack and `FlightPath` ignores height, so every brush went to the top. Fix:
  `LevelSpawner.FrontCubeNearest(column, height)` walks the front stack (front index to
  the next row boundary, top first) and returns the cube nearest a height; the director
  takes the contact's y (the flight is a straight line from the muzzle to the target's
  centre, so the bullet is at the target's height by the time it reaches a neighbour).
  Also found while tracing: the flight loop exited on completion without a sweep at
  progress 1, and with the slots this far below the board every neighbour is entered in
  the last few percent, so a final `BrushAlongFlight(..., 1f, ...)` now runs after the
  loop. **Play verified** with a flight trace (bullet spawn and landing per shot, every
  cube's yaw per frame) and the seated shooter teleported to x=+4 so its flights cross the
  columns between: shots at column 2's top / middle / ground brushed column 3 at y 2.25 /
  1.35 / 0.45, then column 4's stack brushed column 5 the same way, six for six.
  **Geometry finding, the real reason brushes are rare from a near slot:** a flight from
  slot 0 to a settled front-row cube is steep enough to clip nothing (x at the neighbour's
  front edge lands outside its footprint by a few hundredths); the only brushes a near slot
  gets are on the first shot at a stack still sliding in (target further back, shallower
  line). Far slots shooting across the board brush every column between, which is where
  Salih saw the top-only bug. **Probe trap:** a "new lean" detector keyed on yaw crossing a
  threshold fires twice per shove - once on the swing, once on the counter-swing eleven
  frames later - and reads as two brushes of the same cube; pair it with the bullet trace.
- The target takes the shove too (2026-09-07) - done, 109/109 (no new test: director glue,
  the nudge itself is pinned in CubeViewTests). Salih: the bullet hits the target hardest
  of all, so it should get the same shove. `ShotVisual` now shoves the popped cube at
  impact with the contact where the flight enters its footprint, through a new `Shove`
  helper the brush sweep shares; the collapse only scales, so the two stack, and
  `Destroy` kills the shove with the cube. Salih also asked whether `MuzzleReach` 1
  makes flights cross more neighbours: no - reach moves the muzzle along the same aim
  line, the line itself is set by slot depth against the board; a `Brush.Margin` knob
  (footprint widened by a few tenths) was offered and declined. **Play verified**, twelve
  shots from four seated shooters: every target shifted 0.147-0.149 during its collapse
  (Push 0.2 in Salih's scene, the curve's sampled peak), yaw 0.0 on eleven of them and
  11.5 on the one hit mid-slide; zero errors. **Why the target does not spin:** the
  bullet flies to the cube's centre, so the contact on the face is exactly opposite the
  centre from the aim and the lever `arm x travel` is zero - a straight punch pushes and
  does not turn. Only a target still sliding (hit off-centre) spins. Left as is; a
  deliberate off-centre impact would be the change if Salih wants the target to turn.
- Post-delivery fix: a shooter tapped mid-step revealed the NEXT shooter's colour
  (2026-09-16) - done, 110/110 (+1 `LevelSpawnerTests.PoppingAShooterMidStep_
  RevealsItsOwnColour_NotTheNextShooters`). Salih found it in Play, nine days after the
  mail: spamming a column, the hidden shooter tapped before it reached the front revealed
  blue (the shooter behind it), then fired at green cubes (the domain was right). Shipped
  since 8ce133a (2026-09-05). **Root cause:** `OnSelected` takes from the domain BEFORE
  `PopFrontShooter`, and the pop force-completes the pending step so its arrival reveal
  fires; that reveal read `_shooters.Peek(column, 0)` at arrival, by which time the domain's
  front was already the shooter behind. Not hidden-specific: any second tap inside one
  `StepDuration` dressed the stepping shooter in the next one's colour and ammo counter.
  **Fix:** `StepQueueForward` reads the arriving shooter when it schedules the step (the
  domain front IS that shooter at that moment) and the closure reveals that view with that
  `Shooter`; `RevealFront(column)` became `Reveal(view, shooter)`. Same allocation budget:
  one closure per selection. **Why the suite missed it:** the existing mid-step test never
  advanced the domain queue (it called Pop/Step without `TakeFront`) and used one material
  for every colour, so a wrong colour was invisible. The new test mirrors the director's
  order and uses a per-colour table (`PerColourMaterials`). Play verification pending.
