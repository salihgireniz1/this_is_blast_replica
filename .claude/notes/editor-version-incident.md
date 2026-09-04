# Editor version incident (6000.5 -> 6000.0.68f1, twice)

Moved out of `CLAUDE.md` on 2026-09-04. Read when the Editor boots in Safe Mode, when `git status` shows `ProjectVersion.txt` changed, or before opening the project from the Hub.

Unity **6000.0.68f1** — the project's version, fixed. It was moved here from 6000.5.7f1 on
2026-08-30 and stays. Unity does not support downgrading, so that move left three kinds of
wreckage, all cleared: `manifest.json` asked for packages this editor cannot supply
(URP 17.5, test-framework 1.7, ugui 2.5, multiplayer.center 1.0.1 — every pin is now the
version that actually resolves), three `com.unity.modules.*` entries that exist only in
6000.5 (backup at `Packages/manifest.json.pre-downgrade.bak`), and `Library` caches written
by the newer editor that the older one cannot parse — `ApiUpdater/project-dependencies.graph`
threw `OverflowException` on every import and `expandedItems` failed to load. Both deleted;
Unity regenerates them. A fourth, cleared 2026-09-03: `Assets/Settings/
UniversalRenderPipelineGlobalSettings.asset` was serialized by URP 17.5 and carried nine
settings types 17.0.4 does not have ("Missing types referenced from component
...GlobalSettings" on every load). Deleted and regenerated through URP's own `Ensure`, moved
back to `Assets/Settings`, default volume profile re-bound to the existing one. **Trap:**
deleting the active global settings pops a URP confirmation dialog that freezes every
pipeline command (5 s timeouts on a trivial eval) until Yes is clicked. A fifth, cleared
2026-09-03 the first time a Player was built: both RP assets carried `k_AssetVersion: 13`
(17.5's number) while 17.0.4's last version is 12, and URP's build validator refuses to
build a Player over it ("is not at last version"; the editor itself never complained).
Set to 12 by hand in `Mobile_RPAsset` and `PC_RPAsset` - version 12's fields are a subset of
what the file holds, nothing else changed, and `IsAtLastVersion` reads true after a
reimport. **If a package pin ever disagrees with `packages-lock.json` again,
the manifest is the thing that is wrong.**

**The whole upgrade happened again on 2026-09-04, and here is why — read this before
opening the project from the Hub.** Salih opened it from the Hub UI and the Editor came up
in Safe Mode. Cause chain, every step evidenced in the logs:

1. The Hub reads a project's Editor version from **its own database**
   (`%APPDATA%/UnityHub/projects-v1.json`), never from `ProjectVersion.txt`. The 2026-08-30
   downgrade edited the file, so the Hub's record stayed at `6000.5.7f1` and every Hub
   launch since would have gone there. It did not show until now only because the project
   was always started from the command line with an explicit editor path.
2. Unity normally stops at a modal — *"this project was last opened in a different version,
   continuing may cause irreversible changes"*. It did not appear, because the Hub launches
   this project with `cliArgs: "-automated"` (per-project, stored in
   `%APPDATA%/UnityHub/projectsInfo.json`; our own `unity-mcp.md` asks for the flag so
   dialogs cannot freeze pipeline commands). `-automated` gives `IsHumanControllingUs: 0`
   in the log and auto-answers every dialog, the mismatch warning included. **The flag that
   keeps the pipeline alive is the flag that removed the safety net.** It stays; the Hub
   record being right is what prevents the trigger.
3. 6000.5.7f1 then rewrote `ProjectVersion.txt` and re-resolved packages exactly as before
   (URP 17.0.4 -> 17.5.0, test-framework 1.6 -> 1.7, ugui 2.0 -> 2.5, multiplayer.center
   1.0.0 -> 1.0.1, plus three `com.unity.modules.*` that exist only in 6000.5) — the full
   list is in `Logs/Packages-Update.log`.
4. Safe Mode itself was one file: 6000.5 makes the `instanceId` APIs obsolete-**as-error**,
   and `Assets/Plugins/Easy Save 3/Editor/ES3Postprocessor.cs` uses them at lines 112, 119,
   132 and 147. Do not "fix" ES3 — the case brief fixes the editor at 6000.0.68f1, so the
   wrong editor is the bug.

Recovery, in this order: quit the Editor, `git checkout` the five files 6000.5 touched
(`ProjectVersion.txt`, `manifest.json`, `packages-lock.json`, `ProjectSettings.asset`,
`PackageManagerSettings.asset` — `Game_Scene.unity` is Salih's own uncommitted work, leave
it), delete `Library/ApiUpdater`, `Library/ScriptAssemblies`, the URP 17.5 `PackageCache`
folder and the Library assets the newer editor serialized (`ScriptMapper`,
`BuildSettings.asset`, `EditorUserBuildSettings.asset`, `SpriteAtlasDatabase.asset`,
`SceneVisibilityState.asset`, `expandedItems`, `InspectorExpandedItems.asset` — Unity
recreates every one), then **quit the Hub from the tray** (`minimizeToTray` is on, closing
the window leaves five processes alive that would overwrite the file) and set the project's
`version` to `6000.0.68f1` and `changeset` to `e1e9baaf294b` in `projects-v1.json`. Done
2026-09-04; 88/88 green afterwards, boot clean, no Safe Mode.

**The standing check:** `ProjectVersion.txt` is in git, so a silent upgrade is the first
line of `git status`. That is how this was caught both times — look there before anything
else when the Editor behaves strangely after an open.
