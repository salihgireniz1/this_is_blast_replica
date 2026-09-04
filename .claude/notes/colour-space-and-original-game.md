# Colour space and the shipped original

Moved out of `CLAUDE.md` on 2026-09-04. The phase 0 colour investigation (resolved: the project is Linear because the shipped game is Linear; the clone prototype is gamma and is an art-direction reference only), the TCP2 gain mechanism, the three dead hypotheses, how to drive the reference prototype, and how to read numbers out of the original APK with UnityPy plus every value measured from it.

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

