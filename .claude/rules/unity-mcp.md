# Driving the Unity Editor

The Editor is driven through the Unity CLI (`unity`, v1.0.0-beta.6), which exposes the
same command set two ways: as an MCP server (`unity mcp`, registered as the
`unity-editor-mcp` tools) and as plain shell commands (`unity cmd <name>`).

Unity deprecated the MCP server that shipped inside `com.unity.ai.assistant` and ran in
the Editor process. That is not what this project uses, so there is nothing to migrate.

## Which one to use

- **Running tests → the MCP tool.** It polls `test_status` for you and returns the full
  result in one call. `unity cmd run_tests --async_tests true` returns immediately with
  an empty result set and leaves the polling to the caller.
- **One-shot reads → `unity cmd` from the shell**: `editor_status`, `console`,
  `find_assets`, `eval`. Cheaper in context, because the output can be filtered before
  it is read.

## The test loop

```
recompile { focus: true }
run_tests { filter: "<TestClassName>", mode: "editor", async_tests: true }
```

Recompile first: `run_tests` against stale assemblies reports results for the old code.

## Start the Editor with `-automated`

The pipeline server warns about this at startup: without the flag, any modal dialog blocks
the main thread and every command dies at once, usually as a connection reset rather than a
readable error.

```
"C:\Program Files\Unity\Hub\Editor\6000.0.68f1\Editor\Unity.exe" -projectPath "C:\Users\giren\Desktop\Projects\this_is_blast_replica" -automated
```

Launching from the Hub does not pass the flag, so the Editor has to be started this way.

## When a test result does not match the code you just wrote

`recompile` answering `up_to_date` is **not** proof that your edit was compiled. Assembly
reload can be left locked (`EditorApplication.LockReloadAssemblies` without its matching
unlock, which a package or an interrupted import can do), and then Unity reports
`isCompiling: false`, `autoRefresh: 1` and `up_to_date` while the DLL on disk stays hours
old. Tests run happily against it and report the old behaviour.

Check the source against the assembly before believing a result:

```
ls -l Assets/.../Foo.cs Library/ScriptAssemblies/<Assembly>.dll
```

If the DLL is older than the source, unwedge it:

```
unity cmd eval --code 'UnityEditor.EditorApplication.UnlockReloadAssemblies();
  UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate);
  UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(); return "ok";'
```

Then poll the DLL's timestamp, not `recompile_status`. Hit on 2026-08-31: a red-step probe
came back green because the probe had never been compiled.

**Also do not poll `recompile_status` in an `until` loop that accepts `up_to_date`** — the
first poll returns the *previous* run's state and the loop exits before the new compile even
starts. Wait for `completed`, or watch the DLL timestamp.

## When every command times out

Every pipeline command runs on the Editor's main thread, so **any modal dialog freezes
all of them** — an unsaved-scene prompt, an import dialog, a script-reload prompt.
`editor_status` timing out after 30 s while the Editor process is alive means a dialog is
waiting for a human. Ask Salih to look at the Editor window rather than retrying or
restarting anything.

## Reading failures

`unity cmd console` or `get_console_logs { severity: "error" }` for compile errors. Unity
keeps old entries in the log file, so check timestamps before believing an error is
current.
