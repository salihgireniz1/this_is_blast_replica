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

House rules live in `.claude/rules/`: how we work chunk by chunk, the code standard,
and how the Unity Editor is driven.

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

Unity **6000.0.68f1** — the project's version, fixed. It was moved here from 6000.5.7f1 on
2026-08-30 and stays. Unity does not support downgrading, so that move left three kinds of
wreckage, all cleared: `manifest.json` asked for packages this editor cannot supply
(URP 17.5, test-framework 1.7, ugui 2.5, multiplayer.center 1.0.1 — every pin is now the
version that actually resolves), three `com.unity.modules.*` entries that exist only in
6000.5 (backup at `Packages/manifest.json.pre-downgrade.bak`), and `Library` caches written
by the newer editor that the older one cannot parse — `ApiUpdater/project-dependencies.graph`
threw `OverflowException` on every import and `expandedItems` failed to load. Both deleted;
Unity regenerates them. **If a package pin ever disagrees with `packages-lock.json` again,
the manifest is the thing that is wrong.**

Stack: URP **17.0.4** linear, VContainer, R3, UniTask, DOTween, LeanTouch+, Addressables,
Toony Colors Pro 2 Hybrid Shader 2.

## Performance targets

120 FPS / 8.33 ms, **0 B GC alloc per frame** during gameplay, worst case 360 cubes.
Phases 1-2 may stay deliberately naive; phase 5 is the measured pass and the naive
version is the "before" half of the evidence.

## Status

**THE CASE IS LIVE — read this before anything below.** The interview case from Apps
(apps.com.tr) arrived 2026-09-01 and is built **in this project**. Deadline: **Monday
2026-09-07**, delivered by mail after the final push. The repo is
`github.com/salihgireniz1/this_is_blast_replica` (private, `info@apps.com.tr` invited).
Brief: `Game Developer Case.pptx` in Downloads; full spec digest in the auto-memory file
`case-is-live-in-this-repo.md`. The short version: This is Blast core WITHOUT merge,
10x10 cube grid, 5 slots, top shooter row selectable + 2 queue rows visible, Hidden
Shooter feature, JSON-driven sample level (solvable, all 5 colors, includes a hidden),
win/fail overlays with restart, direct play on Editor Play, Unity 6000.0.68f1 + URP.
Evaluation order: bug-free > juiciness > architecture > performance > git usage.

**Case status:**
- Art swap — done, 22/22 green. `AppsAssets.unitypackage` imported (their WalkingCube
  shooter, Gun, Cube.fbx, per-color materials + Hidden + Outline, self-contained TCP2
  `CustomShader`, SplashEffect, Splash.wav, Baloo2 font). `Palette.asset` rows now bind
  the AppsAssets materials (`Surprise` → `Cube_Hidden`), tints synced from each
  material's `_BaseColor`. Deleted: `00_GAME/Materials`, `00_GAME/Meshes` (RoundedCube),
  `JMO Assets` (49MB paid TCP2 install — their materials bind the generated shader inside
  AppsAssets, nothing needed the store install). Scene keeps the camera/light/volume rig,
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
- Next: the slot row (5 slots, occupy/leave rules), then the firing rule and win/fail
  detection, then the JSON level format, then presentation.

The phase plan below is the **portfolio** plan. It resumes after the case ships; the case
overrides it wherever they disagree (no merge feature, their art, 10x10 single layer).

**Phase 0 — infrastructure**

- 0.1 packages + `UNITASK_DOTWEEN_SUPPORT` define — done
- 0.2 asmdef skeleton + `ArchitectureTests` — done, 3/3 green
- 0.3 `BlastColor` enum + `PaletteData` SO + `Palette.asset` — done, 11/11 green
- 0.4 `JuiceConfig` SO + `Juice.asset` — done, 13/13 green (cube group only; the cannon,
  fire, merge and win groups join in the phase that first animates them)
- 0.5 ProjectSettings + `Mobile_RPAsset` — done. `Mobile` is the selected quality level
  and `Mobile_RPAsset` the pipeline everything renders, profiles and screenshots through;
  `PC_RPAsset` is left alone on purpose, nothing points at it. `ProjectSettingsTests` was
  written here and deleted again in 0.7.5 — see the note under phase 0.
- 0.6 six cube materials, wired into `Palette.asset` — done. Copied from the reference
  prototype byte for byte, so the tints are the clone's own values; the palette was
  aligned to them, not the other way round.
- 0.7 scene rig + scene materials — in progress:
  - 0.7.1 `Main Camera` per plan B3 — done. Orthographic size 5.53, position
    `(0, 10, -4.5)`, euler `(61, 0, 0)`, solid clear `#93CFFF`, HDR on.
  - 0.7.2 directional light + ambient/fog (B3) — done. Intensity 1, euler `(45, -25, 0)`,
    shadow strength 0.25 / bias 0.3 / normal bias 0.577, soft and realtime; ambient from
    the skybox, fog off. Two traps found while auditing it against the plan, both fixed:
    Unity's new-scene light ships with `useColorTemperature` **on** at 5000K, which was
    warming every surface (the reference prototype has it off), and URP ignores a light's
    own shadow bias while `usePipelineSettings` is on — the inspector would have shown the
    reference numbers while rendering with the pipeline asset's.
  - 0.7.3 Global Volume + post profile (B4) — done. `SampleSceneProfile` renamed to
    `Assets/Settings/Post_Profile.asset` (same GUID, so the scene reference held) and cut
    to exactly three overrides: bloom 0.2 / threshold 1 / scatter 0.5 / HQ filtering,
    vignette 0.15, colour adjustments saturation +5. The URP template's Tonemapping and
    MotionBlur were removed — the reference prototype has no post at all, and a Neutral
    tonemapper would shift every colour in the side-by-side while every material still
    held the reference's values. Reverse that call if the screenshot says otherwise.
    Third trap found here: `VolumeProfile.Add<T>()` creates the override in memory but
    does **not** write it into the asset — it serialised as `fileID: 0` and would have
    vanished on the next domain reload. `AssetDatabase.AddObjectToAsset` is the fix, and
    `Remove` leaves the old sub-asset orphaned in the file, so it needs `DestroyImmediate`.
  - 0.7.4 scene materials (B2) — done. Six materials copied from the reference prototype
    byte for byte, same as the cubes in 0.6: `ground`→`Ground`, `gate`→`Gate`,
    `DocPlatform`→`Dock`, `IdlePlatform`→`Idle`, `projectile`→`Projectile`,
    `trail`→`Trail`. The prototype's `platform` and `tower` are not in the plan's list and
    were left behind. Unity reads the tints back as exactly the B2 hexes, which also
    settles the colour-space question for materials: a Color property is serialised gamma
    encoded and converted on upload, so copying the file is right and re-typing the
    prototype's float values into a linear project's picker would have been the mistake.
    No test: these carry the same shader GUID `CubeMaterialTests` already checks resolves.
    One finding — `Idle`'s alpha 0.73 does nothing. It is opaque in the prototype too
    (`_DstBlend: 0`, `_ZWrite: 1`, queue 2000), so the value reaches the shader and is
    ignored. `Trail` is the only genuinely transparent one (`_DstBlend: 10`, `_ZWrite: 0`,
    queue 3000). Left as the prototype has it; B2's "@ alpha 0.73" describes a value, not
    an effect.
  - 0.7.5 test policy correction — done, 18/18 green. `SceneSetupTests` (15) and
    `ProjectSettingsTests` (4) were deleted and `code-standard.md` rewritten: **asset
    values do not get tests**. Both suites re-asserted serialized constants that already
    live in version-controlled assets, so they caught nothing a diff would not show, while
    failing every time Salih tuned a value on purpose. What they were actually good for was
    a one-time audit against `PLAN.md` — which is what found the three traps above, all now
    recorded here instead. Phase 5's concern (numbers measured under a drifted render scale)
    is answered by the E5 harness stamping render scale, MSAA, shadow distance and the
    pipeline asset into its report, not by an EditMode test.

- 0.8 the phase 0 done-criterion: ground + one test cube, screenshotted against the
  reference — **half done**. Our side is built and captured; the reference side is blocked
  on a decision (below).
  - `Ground`: built-in Plane, position `(0, 0, 0)`, uniform scale **4.03**, `Ground.mat`,
    cast shadows off / receive on — exactly as the reference has it. At 40x40 units it
    fills the whole orthographic frame, so the `#93CFFF` camera clear never shows on
    screen; that is true of the reference too.
  - `Test_Cube`: `RoundedCube.asset` + `Cube_Red.mat`, position `(-1.92, 0.225, 1.339)`
    (plan A2's grid origin), scale `(0.43, 0.45, 0.45)`. The reference prefab also carries
    a child scaled `y = 1.8`, which compensates for its own `.obj` not being a unit cube;
    ours is a clean 1x1x1 mesh, so the root scale alone is correct.
  - The leftover `Cannon` group was **deactivated**, not deleted — it was rendering a
    yellow "20" block into the comparison shot. Phase 2 still owns it.
  - Screenshot: `Docs/Screenshots/phase0_replica.png`, 1080x1920. Screenshots live at the
    repo root, **outside `Assets/`**, so Unity does not import documentation as textures.
  - What the render measures, for comparing later: ground `#648EBE` at centre and
    `#6088B7` in the corner (material tint is `#A3CDEC`, so the lighting takes it down a
    long way), red cube face `#BF2A2F` (tint `#D03131`). The corner-to-centre difference
    is the B4 vignette, and it is mild in numbers but visible as a soft oval across a
    flat single-colour ground — check it against the reference before keeping it.
  - Reference captured: `Docs/Screenshots/phase0_reference.png`, same 1080x1920 frame.
    How, for the next time this is needed (phase 6 and phase 9 both want it): the
    prototype was opened in `6000.0.58f2` (its own `6000.0.54f1` is not in the CLI's
    release feed, and the archive changeset install fails), `com.unity.pipeline` was added
    to its `Packages/manifest.json` (backup at `manifest.json.bak`), and it is then driven
    with `unity cmd --project-path <prototype> ...`. The prototype's scene was stripped to
    ground plus one cube **in memory and never saved**. Note the MCP `unity-editor-mcp`
    tools always target this project — only the `unity cmd --project-path` form reaches
    the other editor.
  - The prototype's own values, read back, confirm every number entered in 0.7.1 and
    0.7.2 exactly: camera `(0, 10, -4.5)` euler `(61, 0, 0)` ortho 5.53 clear `#93CFFF`
    HDR on; one white light, euler `(45, 335, 0)`, intensity 1, colour temperature off,
    soft shadows strength 0.25 / bias 0.3 / normal bias 0.577; ambient skybox at 1, fog
    off. The rig is right. What differs is what it renders.

**The phase 0 finding — RESOLVED 2026-08-31. Read this resolution before the analysis
below it.**

The whole colour investigation was chasing the wrong target. It compared our render against
the **clone prototype**, which is Built-in RP + **gamma**. The shipped original is URP-era
Unity + **linear**. The clone's gamma is not a style choice, it is a deviation from the game
being replicated — so every value copied from it byte for byte carries unknown drift, and
phase 0 was spent trying to reproduce that drift.

Proven by reading the shipped APK (see *Reading the original game* below):
`m_ActiveColorSpace = 1` (Linear), built with Unity **6000.0.63f1** — five patch releases
from this project's 6000.0.68f1, so the same URP generation and the same TCP2 branch.

Our project was found sitting at `m_ActiveColorSpace: 0` (Gamma) while this file claimed it
was linear; Salih had switched it to match the clone, which was the correct action for the
wrong reference. **It is now Linear and stays Linear.** The decision is closed.

Two consequences that change how the remaining phases work:

- **Stop pixel-matching the clone.** Phase 0's done-criterion ("screenshotted against the
  reference") is the cell that created the trap: matching a different renderer pixel for
  pixel is unfalsifiable work with no end. The criterion is now *looks right, and every
  config value has a written reason*. The clone stays useful as an **art-direction**
  reference — Salih likes its look, and that is his call to make — but never as a technical
  baseline.
- **Still to fix:** `m_LightsUseColorTemperature` is `1` here and `false` in the original.
  `m_LightsUseLinearIntensity` already matches at `0`.

The analysis below is kept because the *mechanism* it found is still true and still governs
the art re-tune. Only its target was wrong.

Re-measured 2026-08-30 with both editors driven side by side, ground centre pixel of the
same 1080x1920 frame:

| sample | reference (Built-in, gamma) | ours (URP, linear) |
|---|---|---|
| ground, full | `#9CD8FF` | `#638EBE` |
| ground, ambient only | `#486FA0` | `#3E669A` |
| direct term (full - ambient) | `(84, 105, 95)` | `(37, 40, 36)` |

Ambient is close. The whole gap is the direct term, at roughly 0.4x.

**Where the 0.4 comes from.** TCP2 declares `_HColor` and `_SColor` as Color properties but
uses them as scalar lighting gains:

```
TCP2 Hybrid 2 Include.cginc:1176   half3 highlightColor = _HColor.rgb * lightColor.rgb;
TCP2 Hybrid 2 Include.cginc:1181   ramp = lerp(_SColor.rgb, highlightColor, ramp);
```

A linear project converts a Color property sRGB->linear on upload, so `_HColor` reaches our
shader as `0.2255` where the gamma project's shader sees `0.513`. The output display
transform does not undo it, because the gain multiplies albedo rather than being an albedo
itself. Proven: setting `_HColor` to `0.744` (whose linear value is `0.513`) scaled our
direct term by `2.383 / 2.269 / 2.281` against a predicted `2.267`.

This is the opposite conclusion to 0.7.4's, and both are right. Copying a material file
byte for byte is correct for `_BaseColor`, because albedo survives the round trip. It is
wrong for `_HColor` and `_SColor`, because a gain does not.

**But no single value reproduces the reference.** To land on `#9CD8FF` our shader would need
`_HColor` of `0.840 / 0.911 / 0.799` — per channel, not one scalar — while ambient sits
separately at 86% / 92% / 96% of the reference. The mismatch is spread unevenly across every
term, which is the signature of a colour-space difference rather than one wrong property.
The choice this paragraph used to pose — re-tune the art in linear, or move to gamma — was
settled by the original: **linear, re-tune the art.** The measured tints and ramp values to
re-tune *to* are in *Reading the original game* below. Plan B1's guess of `_RampSmoothing`
~1.2 is wrong; the shipped value is **0.723**.

**Three hypotheses are dead. Do not re-run them.**

1. *The toon ramp is on its shadow side.* No. The ramp input is half-lambert, not `N.L`:
   `ndlWrapped = ndl * 0.5 + 0.5` (`Include.cginc:1153`), so `N.L = 0.707` arrives as
   `0.8536` against `Ground.mat`'s actual `_RampThreshold` of `0.846` — the lit side. The
   `0.75` this file used to quote was the shader's default, never the material's value,
   which is why the probe that moved the threshold to `0.5` changed nothing.
2. *Attenuation is zero, collapsing the ramp to `_SColor`.* No. Setting `_SColor` to magenta
   turned only the cube's cast shadow magenta; the open ground did not move. `atten = 1`.
3. *Gamma versus linear changes the ambient SH.* No. Both projects report the same
   `RenderSettings.ambientProbe[0,0]` — reference `0.1802838`, ours `0.1815462`. Switching
   our `ambientMode` to Flat does hide the gap, but by pumping in extra ambient, not by
   removing the cause. It was reverted.

Also dead, from an earlier session: the prototype's materials carry both `_BaseColor` and
`_Color` with different values. `_Color` is dead data left by a previous shader and kept in
`m_SavedProperties`; TCP2 Hybrid 2 reads only `_BaseColor`, in the `.cginc` both pipelines
share (`TCP2 Hybrid 2 Include.cginc:825`). The palette values from 0.3 and 0.6 are correct.

**The original inverts this, and the inversion must not be copied.** Its cube shader is
`Toony Colors Pro 2/User/TIB_Shader_Unit` — a variant produced by TCP2's shader generator,
with 12 properties. Its property list contains `_Color` and does **not** contain
`_BaseColor`, so there the live tint is `_Color` and `_BaseColor` is the dead leftover
(identical `#FD4DCA` across all 18 of its materials, which is the tell). This project uses
**stock** `Hybrid Shader 2`, which reads `_BaseColor`. So: **take the original's values,
never its property name.** Write them into `_BaseColor` here.

**Scene values corrected and saved.** `Game_Scene` had drifted and this file described values
it no longer held — the doc said B3, the file said otherwise. Now matching the reference
exactly: `ambientMode` Skybox (was Flat), `shadowStrength` 0.25 (was 0.911), `shadowBias` 0.3
(was 0). Camera, light angle, intensity, normal bias, fog and skybox already matched. The one
remaining difference is camera `far` — 20 here against 1000 there — which is framing, not
colour, and was left alone.

**Every number above was measured on Unity 6000.5.7f1 with URP 17.5.** The project now runs
6000.0.68f1 with URP 17.0.4, and the TCP2 shader branches on `URP_VERSION`. Re-measure before
acting on any of it.

**Driving the reference project**, since phase 6 and phase 9 both need this comparison
again: it is open in `6000.0.58f2` (its own `6000.0.54f1` is not in the CLI's release feed
and the archive changeset install fails), `com.unity.pipeline` was added to its
`Packages/manifest.json` (backup at `manifest.json.bak`), and it answers to
`unity cmd --project-path <prototype> ...`. Note the MCP `unity-editor-mcp` tools always
target *this* project — only the `unity cmd --project-path` form reaches the other editor.

**Reading the original game (added 2026-08-31).** The shipped build is at
`This+is+Blast!_3.20.0_APKPure.xapk` in the repo root — `com.kiragan.blastjam`, v3.20.0,
build 519, Kiragan Games. It is the **technical** ground truth and it outranks the clone
prototype on every question of configuration. The clone remains the art-direction reference.

*The standing rule for it:* **read numbers, never take assets.** Configuration, material
property values, transforms, timings and data schemas are measurements — the same category
as reading a pixel off a screenshot, and they are what this game's look actually consists
of. Meshes, textures and audio do not enter this repository, and neither does the level
content: the campaign is a `PLAN.md` deliverable, and analysing the original's difficulty
curve to design our own is both the stronger portfolio claim and the thing that keeps that
deliverable ours.

*How to open it,* since phases 2, 4, 6 and 7 all want it again:

```
unzip <xapk>                      # an XAPK is a zip: base APK + asset packs
  com.kiragan.blastjam.apk        #   assets/bin/Data/data.unity3d  (boot: settings, shaders)
  UnityDataAssetPack.apk          #   assets/bin/Data/datapack.unity3d  (materials, meshes)
                                  #   assets/aa/Android/*.bundle        (Addressables)
  config.arm64_v8a.apk            #   libil2cpp.so + global-metadata.dat
```

Then `UnityPy` (already installed, 1.25.3) parses the serialized files. Two traps, both hit:
UnityPy's bundled type schema is 4 bytes short for 6000.0.63, so **every** read needs
`o.read_typetree(check_read=False)`; and `m_SavedProperties` entries come back as `(key,
value)` **tuples**, not only as `{"first","second"}` dicts — handle both or every property
silently reads as absent.

*What it is built from:* Unity **6000.0.63f1**, **Linear** colour space,
`m_LightsUseLinearIntensity` false, `m_LightsUseColorTemperature` false. Cubes use
`TCP2/User/TIB_Shader_Unit`, cannons `TCP2/User/TIB_Shader_Shooter_NS` — custom TCP2
generator variants, not the stock Hybrid Shader 2 this project uses. Whether it runs URP or
Built-in was **not** established; `m_DefaultRenderPipeline` was not readable from the boot
bundle. Shader Graph shaders are present, which leans URP, but treat it as open.

*Measured cube values,* for the phase 6 re-tune. These are the original's `_Color`; they go
into our `_BaseColor` (see the property-name warning above):

| BlastColor | tint | `_SColor` |
|---|---|---|
| Yellow | `#FFBE00` | `#D65B00` |
| Red | `#D80D01` | `#460202` |
| Blue | `#009FFF` | `#1A1A93` |
| Green | `#21CC1C` | `#135704` |
| Orange | `#FF8423` | `#951100` |

`_HColor` is pure white on every material. Ramp is `_RampThreshold` **0.592**,
`_RampSmoothing` **0.723**, `_RampType` 2 — Blue alone differs at 0.624 / 0.627. Also on:
`_UseMatCap` 1 with a matcap texture (this, not real reflections, is where the glossy
plastic look comes from), `_UseEmission` 1, `_UseMobileMode` 1, `_ReceiveShadowsOff` 1
(cubes receive no shadows). Off: `_UseOutline`, `_UseSpecular`, `_UseRim`, `_UseReflections`.

*The palette question from phase 1 is settled by it.* There are **16 colour slots**, unit and
shooter authored as separate material sets — and 12 of the 16 are bit-identical across the
two, the four that differ (Blue, Orange, Brown, Purple) differing by a hair that reads as
drift rather than intent. So "one palette serves both cubes and cannons" is confirmed.
`Secret_Shooter_AB` exists with no unit counterpart, which is the `Surprise` asymmetry this
file already records. Sixteen slots against our five also vindicates
`Surprise = byte.MaxValue`: colours really do get appended.

*Addressables group map,* which is the feature list of the shipped meta and a plan for D7 and
phase 4: `levels`, `levelrewards`, `common`, `extrapowerup`, `gameonboarding` +
`gameonboarding_ab` + `gameonboarding_revamp` (they A/B test onboarding), `oocoffer`,
`seasonpass`, `streaktournament`, `weeklymissions`, plus per-language
`localizationfallbackfonts`.

- 0.9 `DOTweenSettings.asset` aligned with plan E4 — done. Compared field by field with the
  reference prototype's: everything already matched, `defaultEaseType 6` (OutQuad) and
  `defaultAutoKill 1` included. Two values differ from the prototype **on purpose**, and
  both are now set: `useSafeMode 1` (the prototype runs 0, which hides tweening a destroyed
  object rather than fixing it) and `defaultRecyclable 1` (tween objects pooled, so mid-game
  allocation stays at zero). `logBehaviour` also moved to `ErrorsOnly`. The runtime half of
  E4 — `DOTween.Init(...)` and `SetTweensCapacity(1500, 100)` — is code and belongs in
  `GameLifetimeScope`, not in this asset; the asset is aligned so the editor and a build do
  not behave differently. No test: asset values.

- 0.10 `TweenEngineSetup` — **built, then deleted.** Plan E4 calls for a runtime half
  (`DOTween.Init(...)` + `SetTweensCapacity(...)`) and it was written literally. Held against
  `DOTweenSettings.asset` afterwards, three of its four lines were already in the asset from
  0.9 — `useSafeMode: 1`, `logBehaviour: 2`, `defaultRecyclable: 1` — and DOTween reads that
  asset itself the first time anything tweens. So `Init` plus the three assignments re-typed
  at runtime what the project already ships. **The plan's E4 runtime half is redundant with
  its own asset half; do not write it again.**
  What survives: `SetTweensCapacity(1500, 100)` is the one setting with no field in the asset,
  and it goes into `GameLifetimeScope` as a single line in 0.11. The 1500 is a guess off the
  360-cube worst case — phase 5 is where it gets measured.
  **The finding worth keeping:** `DOTween.Init` is a one-shot. DOTween brings itself up from
  the asset the moment anything tweens, and every later `Init` is ignored with no error, no
  warning and no log. Anything that must hold regardless has to be assigned *after* the call,
  never passed as an argument to it.

- 0.10b test cull — done, 12/12 green. `TweenEngineSetupTests` and `BlastColorTests` were
  deleted. Both were written earlier in this phase and both failed the standard in
  `code-standard.md` on a second read:
  - `TweenEngineSetupTests` asserted `DOTween.defaultRecyclable`, `useSafeMode` and
    `logBehaviour` immediately after `Apply` assigned those exact three fields two lines
    earlier. That is the assignment operator under test. The one-shot trap it claimed to
    guard cannot fire while those assignments exist, and deleting them is a **choice**
    visible in the diff — so the trap belongs in the comment above them, which is where it
    now lives.
  - `BlastColorTests` protected the wrong thing. The real hazard is somebody inserting a
    colour mid-enum (`Purple = 2`, Blue pushed to 3), which silently repaints every authored
    level — and `PlayableValues_AreContiguousFromZero` stays **green** through exactly that.
    Meanwhile it goes red on a harmless gap (`Blue = 3`, 2 left free), which breaks no level
    file. The other two tests read `: byte` and `= byte.MaxValue` back off the declaration.
    Nothing yet reads a `BlastColor` as a raw number — no `LevelDefinition`, no save file —
    so the contract it guards does not exist yet.
    **Bring it back in phase 1**, with the level format, as one test that pins the actual
    numbers (`Yellow == 0`, `Red == 1`, `Blue == 2`, `Green == 3`, `Orange == 4`). That one
    fails on the insertion, which is the mistake worth catching.
  - `CubeMaterialTests` went too, folded rather than dropped. Both of its tests found the
    materials by the filename convention `Cube_{color}.mat` — a convention **no code reads**,
    since `Palette.asset` holds direct references. So they guarded a contract that does not
    exist, and the first one merely repeated the palette's own material check. The second had
    a real idea, and it moved into the loop `PaletteAssetTests` was already running:
    `EveryPalette_HasARenderableMaterialForEveryBlastColor` now asserts the material exists
    **and** is still bound to `Toony Colors Pro 2/Hybrid Shader 2`. That check earns its place
    because an unresolved shader GUID makes Unity swap in the error shader silently — magenta
    cubes at build time, never in the editor.
  The lesson generalises past these files: a test written in the same turn as the code tends to
  mirror the code. Re-read it a phase later and ask what mistake makes it red.

- 0.10c dead code cull — done, 10/10 green. `~Utils/RoundedBox.cs` deleted: 86 lines with zero
  callers, the only file in the project outside the asmdef layering (no asmdef, global
  namespace, compiled into `Assembly-CSharp` where `ArchitectureTests` cannot see it). It was a
  one-shot generator and its output is committed — `Assets/00_GAME/Meshes/RoundedCube.asset` is
  a real serialized `Mesh` (`!u!43`, 202 KB, vertex data baked in, own GUID), so the scene binds
  to the asset and nothing depended on the script. **Where `RoundedCube.asset` came from:** a
  procedural rounded-box generator — a cube with edges and corners filleted by a radius, flat
  face regions left exactly flat, built on a uniform grid. If a different radius is ever wanted
  that is a phase 6 art decision and gets written fresh.

- 0.11 `GameLifetimeScope` (Bootstrap) — done, 12/12 green. The composition root. It registers
  `Palette.asset` and `Juice.asset` as instances and calls
  `DOTween.SetTweensCapacity(1500, 100)` — the one line of plan E4 with no field in
  `DOTweenSettings.asset`. Lives on a `GameLifetimeScope` GameObject in `Game_Scene`, with both
  assets assigned in the inspector. `Blast.Bootstrap` gained a `VContainer` reference, and the
  test assembly gained `Blast.Bootstrap` + `VContainer`.
  **What the test does and does not do:** it asserts the scene holds exactly one scope and that
  both serialized fields are assigned. It deliberately does **not** resolve anything from the
  container — "I registered X, is X registered" restates the code, which is the pattern 0.10b
  deleted three suites for. What it catches is the real silent failure: an unassigned inspector
  reference registers `null` without a word, the container builds fine, the scene loads fine,
  and the throw arrives a phase later deep inside gameplay. Proven red first: 0/2, count 0.
  The assets arrive through the inspector rather than `Resources.Load` or a path constant
  because a path is a string nothing verifies and it resolves at runtime in a build, while a
  serialized reference is checked by Unity, survives a rename, and a test can see it.
  **Oddity, watch for it again:** the file was overwritten by VContainer's empty `LifetimeScope`
  template about a second after it was written (the `.meta` is stamped one second *before* the
  `.cs`). Rewritten and it held the second time. If it recurs, find what is regenerating it
  before writing the file again.

**Deferred on purpose, with the trigger written down** — a boot scene plus a root/child scope
split. Salih raised it here and the destination is right: application-lifetime services in a
root scope that survives scene loads, level-lifetime registrations in a child scope that dies
with the level. Not built yet because the project has **one scene**, so today it would be an
empty scene whose only job is to load another one. Deferring is cheap for the reason DI exists:
moving a registration from one scope to another touches no injection site at all.
**Trigger: the moment a second scene exists** — the level-to-level transition in phase 3 or the
menu in phase 4. `ILevelRepository`, which holds Addressables handles across a level change, is
the first thing that outlives a scene and so the root scope's first real resident.
Separately, and worth keeping straight: "managers" do **not** motivate that scene. Plan D2 is
explicit — one composition root, zero `FindObjectOfType`, zero singletons — and D2b's
`IAudioService` / `ISaveStore` / `IGameClock` are plain C# classes registered in the container,
not MonoBehaviours on a `DontDestroyOnLoad` object. Putting them in the scene would hand their
lifetime to Unity and cost the substitution the whole test strategy runs on (`ManualClock`
verifying a three-second behaviour without waiting three seconds).

Phase 0 is now complete except Addressables groups (plan D7), which are **deferred out of the
phase on purpose**: there is nothing to put in them yet, no
`LevelDefinition` and no prefabs, so the groups get created in the phase that first has an
asset to address (`Levels` in phase 1, `Gameplay` in phase 2).
The colour gap between our render and the clone prototype is **closed, not parked**: the
project is Linear because the shipped original is Linear, and the art gets re-tuned to the
original's measured values rather than matched pixel for pixel against the clone. See the
phase 0 finding above for the resolution, the mechanism, and the three dead hypotheses.

**Naming, settled 2026-08-31 so phase 7 does not reopen it.** Two different things live at a
board position and they never share a word:

| concept | type | holds |
|---|---|---|
| the address — *where* | `Cell` | `Column`, `Row`, `Layer` |
| the contents — *what* | `Cube` (phase 7) | `Color`, `Hp`, `Hidden` |

`board.Get(new Cell(3, 4, 0))` returns the cube standing there. **`BoardCell` was rejected**:
it repeats "Cell" for the thing that is explicitly not a cell, which is the exact confusion
the split exists to prevent. `Cube` is the game's own word (the plan says "küp" throughout,
the prototype has `ColorCube`) and it completes an family the plan already names —
`Cube` (Domain) → `ICubeView` / `CubeView` (Presentation), `ICubeFactory`, `ObjectPool<CubeView>`.
`Coord` + `Cell` was rejected too: it is the more conventional pairing, but it would rename the
address type a second time, back to the positional name that `GridPos` already was.

The rule behind it: an address must be value-equal by coordinate alone, because `Cell` is a
dictionary key. Folding mutable contents into it would change a key's identity when a cube
recolours, and the entry would go missing — which is what `Equals`/`GetHashCode` in 1.1 exist
to prevent.

Until phase 7 there is no contents type at all: the board is a flat `BlastColor[]` and `Cell`
only produces the index into it (`layer * rows * cols + row * cols + col`). Swapping that array
for `Cube[]` later stays inside `Get`/`Set`; no caller changes.

**Phase 1 — in progress**

- 1.1 `Cell` (Domain) — done, 14/14 green. Named `GridPos` when written and renamed on
  2026-08-31 at Salih's call; the file, the type and the test moved together and Unity's
  `rename_asset` kept both GUIDs. The header now says out loud what the rename risks blurring:
  a `Cell` is **where**, never **what** — the colour standing on it belongs to `BoardModel`,
  keyed by this address. `IEquatable<Cell>` plus `Equals(Cell)`,
  `Equals(object)`, `GetHashCode` and `==` / `!=`. Both reds it was written against were real:
  `ValueType.Equals` boxes both operands on every comparison (`Cell` is a dictionary key in
  a project budgeted at 0 B/frame, and nothing in the source shows it), and
  `ValueType.GetHashCode` hashed `(3,4)` and `(4,3)` into the same bucket.
  `HashCode.Combine` is the hash — order-sensitive, allocation-free, and already in the
  netstandard2.1 profile, so no hand-rolled `x * 397 ^ y`. Its seed is randomised per process,
  which is fine because nothing persists a hash; **if a hash ever has to survive a save file,
  this is the line that has to change.**
  `ToString` was deliberately left out. It would make a future assert message readable, but no
  test needs it today.
  The test file needed one repair before it could ever go green:
  `Assert.That(() => _sink = left.Equals(right), ...)` bound `ActualValueDelegate<bool>`
  instead of the `TestDelegate` the constraint needs, because an assignment expression yields
  a value — so it threw `ArgumentException` before measuring anything. It is a statement
  lambda now, with the reason in a comment above it.

- 1.1b `Cell` gains `Layer` — done, 16/16 green. The board is 3D: plan A1 stacks one to three
  cubes per column and row (`Grid Y` steps 0.225 / 0.625 / 1.025) and plan E3 indexes it
  `layer * rows * cols + row * cols + col`. `Cell` was written with two axes and was simply
  incomplete. `Layer` folds into `Equals` and into `HashCode.Combine(Column, Row, Layer)`.
  **The finding worth keeping:** four tests were written and two of them were green while the
  bug was live. A column-vs-layer pair `Cell(3,0,4)` against `Cell(4,0,3)` already differs in
  its column, so it passes whether or not `Layer` is in the hash at all — the name claimed one
  thing and the assertion exercised another. Both were deleted. Only two pairs earn a run:
  a transposed column/row (catches a symmetric hash) and a stacked pair `(3,4,0)` vs `(3,4,1)`
  with the other two axes held equal (catches a dropped field). The rule this keeps proving:
  when a test passes, check that it would have failed.

- 1.2 `BlastColorTests` — done, 15/15 green. Brought back from the note in 0.10b, now that
  1.3's `BoardModel` is about to store `BlastColor` and the level format will store it raw.
  One test, pinning the numbers: `Yellow == 0`, `Red == 1`, `Blue == 2`, `Green == 3`,
  `Orange == 4`, `Surprise == byte.MaxValue`. Appending a sixth colour is a **choice** and
  leaves it green; renumbering an existing one is a **mistake** and turns it red.
  It replaces the deleted `PlayableValues_AreContiguousFromZero`, which stayed green through
  exactly the insertion that matters and went red on a harmless gap.
  Shown red the only way a pinning test can be: `Purple = 2` was inserted mid-enum, the suite
  went 10/15 with `BlastColorTests` reporting `Blue moved.` (the four `PaletteAssetTests`
  reds were the same probe's collateral — no palette row for Purple), then the probe was
  reverted. The enum's own `: byte` width and `Surprise`'s position are still untested on
  purpose: both are read straight off the declaration, so a test would only restate it.

The plan's 1.2 slot (`CubeColor` enum + `PaletteData` SO) was already delivered in 0.3, so
this number was free.

**Input for 1.3, measured from the original's level bundle 2026-08-31.** Its campaign is
**2000 levels** (numbered 1-2000, one absent) across 2447 assets — so roughly 450 levels ship
a second variant, which is what an A/B test of a level looks like. Each level asset carries:

```
difficultyLevel : int
profiles        : list
myStack         : { colorProfile, width, height, space, size, dynamicBoard }
myStage         : { colorProfile, width, height, space, size, zOffset }
```

**Which grid is which, corrected.** A first reading of these names guessed `myStage` was the
target board. It is the other way round, and the fuller field list settles it:

- **`myStack` is the target board.** `width` is 10 in 1993 of the 1999 levels, which is plan
  A1's ten columns; it carries `StackHeight`, `LevelTargets` and `dynamicBoard`.
- **`myStage` is the player's side.** `width` 3, and it carries `deckZOffset` (the dock),
  `PoolCount`, `PoolVolume`, `WallsOffset`, `FirstRowOffset`.

An earlier note here also claimed `zOffset` appears on one and not the other. Both carry it.
That claim came from a probe that printed only the first six keys of each dict — a conclusion
drawn from truncated output, which is the mistake, not the field.

Four things this establishes, all measured rather than inferred:

1. **There are two grids, not one**, sized independently. `BoardModel` models the target board
   alone; the dock and the cannon queues are separate types. Do not widen one array for both.
2. **The starting board is a filled rectangle.** `LevelTargets` is empty in **all 1999**
   levels and there is no per-cell colour or occupancy data anywhere in the format. A level is
   `width` x `height` x `colorProfile` x `StackHeight` and nothing else, which is how 2000
   levels fit in 970 KB. Holes are never authored — every gap is made at runtime by a cube
   dying, and plan A4 closes it again by shifting the column forward.
3. **Layers are almost unused.** `StackHeight` is `1-1` on **1958 of 1999** levels; 28 sit at
   `2-2`, three at `3-3`, two at `4-4`, and eight hold mismatched pairs (`1-2`, but also `3-1`
   and `4-1`, which break the min/max reading of that `Vector2` — its meaning is not
   established). Plan A1 says one to three layers; the shipped content says one, 98% of the
   time. `Cell.Layer` still earns its place for the other 2%, but nothing hot depends on it.
4. **Stacks empty from the top and never fall.** Confirmed against the shipped game by Salih
   on 2026-08-31, and it agrees with plan A1's target order `x` ascending / **`y` descending**
   and with A4's "no falling, only a forward shift". The two rules are consistent for a reason
   worth writing down: taking the top cube first means a stack is always contiguous from the
   ground up, so no cube is ever left floating and gravity never has to exist. A7 needs no new
   row for this — the clone got it right.

- 1.3 `BoardModel` (Domain) — done, 18/18 green. The stage: a flat `BlastColor[]`, `Get` and
  `Set` keyed by `Cell`, and a private `IndexOf` that resolves
  `layer * Rows * Columns + row * Columns + column` and refuses anything off the board.
  **Scoped to the target board only** (`myStack` in the shipped format, see the correction
  above) — the dock and the queues are differently shaped and get their own types.
  Two tests, both aimed at a silent failure. `EveryCell_HasItsOwnSlot` writes each address in
  turn and reads every other one back, so a stride mistake that lands two cells in one slot is
  caught; a plain write-then-read-the-same-cell round trip was deliberately **not** written,
  because it passes under any formula including a transposed one. `AnAddressOutsideTheBoard_IsRefused`
  uses column `Columns` rather than a wild number on purpose: on a 2x3x4 board that resolves to
  flat index 2, a perfectly valid slot holding `(0, 1, 0)`, so without its own bounds check
  `Get` answers with a neighbour's colour and never throws. A wildly out-of-range column would
  run off the array and throw by itself, letting a missing check pass.
  The bounds check casts each axis to `uint` so one comparison covers both ends — a negative
  index wraps to something enormous and fails the same test as one past the edge.
  **The finding worth keeping: a red is not proof that your assertion works.** The first
  deliberate break (`row * Rows` for the row stride) did turn the suite red, but with
  `IndexOutOfRangeException` — it overruns the array, so what caught it was the array's own
  bound, not the aliasing logic under test. Only the second break (`layer * Rows`, which
  collides *inside* the array) produced the real message,
  `Writing (1, 1, 0) was visible at (0, 0, 1): two cells share one slot.` When a probe goes
  red, read *how* it went red.
  `Cell` still has no `ToString`, so `BoardModelTests` formats addresses with a local helper.
  Add `ToString` to `Cell` if a second test ever wants it; one caller does not justify it.
  Not built, and not needed yet: constructor validation of the dimensions. Nothing constructs a
  board but the tests, and a zero-sized one already throws on every access through the bounds
  check.

- 1.4 + 1.5 `FrontRow` and `Remove` — done, 22/22 green. **The plan's two chunks were merged,
  on purpose.** `FrontRow(col)` alone returns zero for every column of a fresh board and cannot
  do anything else until something removes a cube, so a test for it could not fail — which the
  code standard forbids. The front index and the thing that advances it are one behaviour.
  **Nothing moves.** Plan A4 has the cubes behind a dead one step forward a slot, but shuffling
  the array would copy a whole column per shot for a result no caller can distinguish. The
  colours stay where they were authored; `_frontRow[column]` walks backwards through them and
  Presentation animates the shift. `_livingLayers[column]` counts what still stands on that
  front row, and `--_livingLayers[column]` doubles as the layer index it hands out, so a stack
  of four gives layer 3 first and layer 0 last — the shipped order.
  `FrontRow` returns `Rows` for a spent column; `Remove` on one throws `InvalidOperationException`.
  **The seam worth remembering:** a cell behind the front still answers `Get` with the colour it
  was authored with. This type tracks no per-cell aliveness, so "is that cube still standing" is
  `FrontRow`'s question and never `Get`'s. It is why no `Empty` member had to be added to
  `BlastColor` and no `Cube` struct had to arrive early.
  Four tests, each on a distinct silent failure: one column's front read through another's, a
  stack emptying from the ground up, a column calling itself empty after `Rows` removals instead
  of `Rows * Layers`, and a spent column handing out one more cube. Deliberately not written:
  that a fresh column reports front zero — true under any implementation.
  **The probe:** removal was flipped to bottom-up (`Layers - _livingLayers[column]--`), which
  leaves every count intact and only changes which cube dies. Exactly one test went red, with
  `Expected layer 3 to go next; the stack is not emptying from the top.` The counting tests
  stayed green, which is the trap that test exists for.
  **Still open, deliberately:** whether a multi-layer column shifts as a whole position or layer
  by layer. The two are identical while `Layers` is 1, which is 1958 of the 1999 shipped levels,
  so the question is left unanswered rather than guessed at.

**`CellTests.Comparing_DoesNotAllocate` is flaky.** Observed on 2026-08-31: it failed once
during a probe run (`Expected: not allocates GC memory`) and passed in the five runs around it,
including three consecutive clean runs immediately after. Nothing in `Cell` had been touched.
The cause is not established — `Is.Not.AllocatingGCMemory()` measures allocation across a
delegate call and a background collection or a first-call JIT inside the window would trip it.
Worth knowing before trusting a single red from it: **check whether it reproduces before
chasing it.** By the standard in `code-standard.md` a test that fails on noise is worse than
one that fails on a choice, so if it recurs, either pin it down or delete it — but the
allocation it guards is real (a boxing `Cell` in a 0 B/frame budget), so do not delete it
casually.

The suite is 22 tests, all green. They are the ones that fail on a mistake rather than on a
choice — `CellTests` (3 — boxing on every comparison and a transposed cell hashing the
same, neither of which is visible in the source), `BoardModelTests` (6 — two cells sharing one
slot, an off-board address answered with a neighbour's colour instead of a throw, one column's
front read through another's, a stack emptied from the ground up, a column calling itself empty
a whole stack early, and a spent column handing out one more cube),
`BlastColorTests` (1 — an existing colour renumbered),
`ArchitectureTests` (3 — asmdef dependency direction and `noEngineReferences`, invisible until
they break), `PaletteAssetTests` (4 — a colour added to the enum but never authored, Unity's
`+` button duplicating the last row, a missing or unshaded material, a material retinted while
its palette row was not), `PaletteDataTests` (2 — real branches: lookup by pairing rather than
by list position, and a throw instead of a silent magenta when the asset has a hole) and
`JuiceConfigTests` (1 — a zero duration makes a tween finish before it is seen, and walking the
serialized tree means every timing field added in a later phase is covered the day it lands) and
`GameLifetimeScopeTests` (2 — the scene's composition root exists exactly once and every asset it
registers was actually dragged in). `TestStub` is gone.

Run them filtered, **and filter by assembly**:
`unity cmd run_tests --mode editor --filter "Blast.Tests" --filter_type assembly --async_tests true`,
then poll `test_status` (the result also lands in `Temp/pipeline_test_status.json`). Without a
filter the run pulls in every package's EditMode suite (Addressables, InputSystem, ...) and
times out at six minutes; filtered by assembly it is under 2 s. The MCP `run_tests` tool and
the default `filter_type` of `testName` were both tried on 2026-08-30 and both timed out at
six minutes without ever starting a run.

`Project_HasExactlyOnePaletteAsset` and `Project_HasExactlyOneJuiceConfigAsset` were deleted
for the same reason the asset-value suites were: they failed on a **choice**. A second palette
is a theme, not a bug - and nothing in the code loads "the" palette by search, so two of them
break nothing. Worse, both suites reached their asset through `.Single()`, so a second one did
not fail one test, it failed all five. Both files now iterate `AssetDatabase.FindAssets` and
assert per asset, with the asset name in every message. Proven both ways before and after:
with a duplicate palette in the project the old suite went 5/5 red, the new one is green at
any count. `[TestCaseSource]` was tried first and rejected - the Test Framework caches the
generated case list, so a deleted asset left a phantom case failing until the cache cleared.
A plain `foreach` inside one `[Test]` has no such state.

`Game_Scene` also holds a `Cannon` / `Cannon_Body` / `Count_Text` group left over from an
early experiment. It is phase 2's subject and is deliberately untouched until then.

Colour identity is `BlastColor` (Domain, `byte`, values written out explicitly because
levels store them raw; `Surprise = byte.MaxValue` so a new colour is always an append).
Colour values live in `Assets/00_GAME/Data/Palette.asset`, edited in the inspector: each row
pairs a `BlastColor` with a tint and a material.

**One palette serves both cubes and cannons, and has to.** Checked against the prototype on
2026-08-31: it has a single `CubeColors` enum, a single `ReferenceManager.GetMaterialForColor`
and a single material set (`cube1_*`), and both `ColorCube.cs:73` (a target) and
`Player.cs:131` (a cannon) call that one method. The deeper reason is the core rule, not the
prototype: the player matches a cannon to a target **by eye**, so separate tints per side would
make the game unreadable. `Surprise` is the one asymmetric row — only a cannon ever wears it
(`Player.cs:123`), no cube does. So the palette is not to be split or renamed per side.

The clone prototype renders in **gamma**; this project and the shipped original both render
in **linear**. Copying a material file byte for byte from the clone is therefore wrong twice
over — once because a Color property is converted sRGB->linear on upload, which is harmless
for albedo and not harmless for `_HColor` and `_SColor` that TCP2 uses as lighting gains, and
once because the clone is a third party's approximation carrying its own drift. Re-tune to
the original's measured values instead; they are tabulated under *Reading the original game*.
