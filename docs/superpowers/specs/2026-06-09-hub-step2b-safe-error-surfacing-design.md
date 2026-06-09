# Step 2b (safe subset) — Surface `SessionResult.Error` + structure post-game cleanup

**Date:** 2026-06-09
**Status:** Approved (design)

## Context

Step 2a extracted the play-orchestration into `IGameSession` and converted the four
`GameConfigViewModel` commands to `ReactiveCommand`. It deliberately left two things for
Step 2b, to be **verified by the maintainer with a real Darkspore run**:

1. Remove `Process.GetCurrentProcess().Kill() //HACK`.
2. Fix the window hide/reshow Avalonia lifetime.
3. Revive the stranded post-game cleanup.
4. Surface `SessionResult.Error` to the user.

The maintainer cannot run the real play flow yet — the new C# server is not connected, so
the kill/reshow behavior cannot be validated. This spec therefore covers **only the subset of
Step 2b that is testable without a live game**: surfacing the launch error (#4) and structuring
the post-game cleanup (#3). The kill **stays on the success path** and the window-lifetime
fix (#1, #2) is deferred until the maintainer can validate against the real server.

**Out of scope (deferred to full Step 2b, needs real run):** removing the kill hack, fixing the
hide/reshow lifetime, wiring the revived cleanup to actually run, constructor-injecting
`IGameSession`.

## Goal

When `IGameSession.PlayAsync` returns a failure, show the error to the user and keep the Hub
alive instead of silently killing the process. Isolate the (currently dead) post-game cleanup
into a named method ready for full Step 2b. The **success path stays byte-identical to today**.

## Current behavior (the problem)

In `GameConfigViewModel.PlayGameWithSave` (tail, ~lines 307-325):

```csharp
if (!result.Success)
{
    //TODO (Step 2b): surface result.Error to the user
}
Process.GetCurrentProcess().Kill(); //HACK
save.ReadFromXml(true);
HubData.Instance.GameConfigs.Remove(this);
HubData.Instance.GameConfigs.Insert(0, this);
_lastLaunchTime = now;
save.LastLaunchTime = now;
Saves.Remove(save);
Saves.Insert(0, save);
SelectedSave = save;
HubData.Instance.Save();
```

The kill runs **unconditionally** — on success and on failure. Everything after it is dead
code. On launch failure the user sees nothing: the process just dies.

## Design

### New component: `OkDialogViewModel` + `OkDialogView`

A single-button informational dialog, following the existing `YesNoDialogViewModel` /
`YesNoDialogView` pattern.

- `src/ReCap.Hub/ViewModels/Dialog/OkDialogViewModel.cs`
  - `public class OkDialogViewModel : MessageDialogViewModelBase<bool>`
  - Constructor `(string title, string content, bool isCloseable = true)` → `base(...)`.
  - `public void OkCommand(object parameter) => CompletionSource.TrySetResult(true);`
  - Namespace `ReCap.Hub.ViewModels` (so the ViewLocator's `"ViewModel"→"View"` global
    replace resolves it to `ReCap.Hub.Views.OkDialogView`).
- `src/ReCap.Hub/Views/Dialog/OkDialogView.axaml` (+ `.axaml.cs`)
  - Copy of `YesNoDialogView.axaml` with a **single** "OK" button bound to `OkCommand`
    (drop the No button; keep the `DialogRoot`/`DialogFooter`/`DialogBody` themes and the
    `Title`/`Content` bindings).
  - `x:Class="ReCap.Hub.Views.OkDialogView"`, namespace `ReCap.Hub.Views`.

### Change: `GameConfigViewModel.PlayGameWithSave` tail

Replace the unconditional kill + dead cleanup with:

```csharp
if (!result.Success)
{
    string message = result.Error?.Message ?? "The game failed to launch.";
    await DialogDisplay.ShowDialog(
        new OkDialogViewModel("Launch failed", message, true));
    return; // Hub stays alive so the user can see the error — no kill on failure.
}

Process.GetCurrentProcess().Kill(); //HACK  (success path unchanged; removed in full Step 2b)
ApplyPostGameSessionState(save, now);
```

### Extract: `ApplyPostGameSessionState`

Move the existing cleanup verbatim into a private method (still called **after** the kill on
the success path, so it remains dead for now — this preserves today's behavior and leaves the
method ready for full Step 2b to wire in before/instead of the kill):

```csharp
private void ApplyPostGameSessionState(SaveGameViewModel save, double now)
{
    save.ReadFromXml(true);
    HubData.Instance.GameConfigs.Remove(this);
    HubData.Instance.GameConfigs.Insert(0, this);
    //TODO: HubData.Instance.SelectedGameConfig = this;
    _lastLaunchTime = now;
    save.LastLaunchTime = now;
    Saves.Remove(save);
    Saves.Insert(0, save);
    SelectedSave = save;
    HubData.Instance.Save();
}
```

## Behavior change summary

| Path | Before | After |
|------|--------|-------|
| Launch success | kill, dead cleanup | kill (unchanged), then dead `ApplyPostGameSessionState` |
| Launch failure | kill (silent death) | OK dialog with error, Hub stays alive (no kill) |

Success path is unchanged. Only the failure path changes — strictly an improvement.

## Known caveat (validate with the real run)

On the failure path the main window may be hidden (`App.OnLocalServerStarted` hides it on
server start). The error dialog is queued by `DialogDisplay.StartRollingDialogs` and renders
once the window reappears via `OnLocalServerExited` → `ShowMainWindow`. Disposing the server
handle on AutoClose (inside `GameSession`'s `finally`) should fire `ServerExited` and reshow
the window, making the dialog visible. This ordering cannot be fully confirmed without a live
run; flagged for the maintainer to verify when the C# server is connected. If the dialog does
not appear, the fix belongs to the deferred window-lifetime work (full Step 2b), not here.

## Testing / verification (no live game required)

- Build `-c Debug_Offline` and `-c Release`: 0 errors.
- Full test suite (`-p:EnableReCapOnline=False`): 20/20, no regressions.
- Smoke-run with 8s watchdog: `STARTED_OK`.
- Manual error-path check (optional, no game server): point a game config at an invalid path so
  launch fails → confirm the OK dialog shows and the Hub process does **not** exit.

No new unit tests: `PlayGameWithSave` is coupled to `HubData`, file I/O, dialogs, and
`Process.Kill` (statics) and is not unit-testable without seams that are out of this subset's
scope. `OkDialogViewModel` is a trivial passthrough over `MessageDialogViewModelBase`.

## Commits

No `Co-Authored-By` / "Generated with" trailer (maintainer standing rule).
