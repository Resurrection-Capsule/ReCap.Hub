# Step 2b (safe subset) — Surface `SessionResult.Error` + structure post-game cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When `IGameSession.PlayAsync` returns a failure, show the error in an OK dialog and keep the Hub alive instead of silently killing the process; isolate the (currently dead) post-game cleanup into a named method ready for full Step 2b — with the **success path byte-identical to today**.

**Architecture:** Add a single-button `OkDialogViewModel`/`OkDialogView` following the existing `YesNoDialog` pattern (resolved by the convention-based `ViewLocator`). In `GameConfigViewModel.PlayGameWithSave`, branch on `result.Success`: on failure show the dialog and `return` (no kill); on success keep the `Process.GetCurrentProcess().Kill() //HACK` and then call the extracted `ApplyPostGameSessionState`. The kill stays on the success path; window-lifetime/kill-removal are deferred to full Step 2b (needs a real Darkspore run).

**Tech Stack:** C# / .NET 10, Avalonia MVVM, ReactiveUI, xUnit. Spec: `docs/superpowers/specs/2026-06-09-hub-step2b-safe-error-surfacing-design.md`.

**Build/test gotchas (unchanged):**
- Build the Hub `-c Debug_Offline` (plain `Debug` errors). Also build `-c Release`.
- Tests: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False`.
- Commits: **no `Co-Authored-By` / coauthor trailer** (standing user order).

**Key facts verified before planning:**
- `ViewLocator.GetViewType()` does `GetType().FullName.Replace("ViewModel","View")` — a **global** replace, so `ReCap.Hub.ViewModels.OkDialogViewModel` resolves to `ReCap.Hub.Views.OkDialogView` (the "ViewModels" namespace segment also becomes "Views"). The View must therefore be `x:Class="ReCap.Hub.Views.OkDialogView"` in namespace `ReCap.Hub.Views`.
- `MessageDialogViewModelBase<T>` ctor is `(string title, string content, bool isCloseable)`; exposes `CompletionSource` (`TaskCompletionSource<T>`), `Title`, `Content`, `IsCloseable`. `YesNoDialogViewModel : MessageDialogViewModelBase<bool>` with `YesCommand`/`NoCommand` calling `CompletionSource.TrySetResult(...)`.
- `DialogDisplay.ShowDialog<T>(IDialogViewModel<T>)` returns `Task<T>`; queues dialogs via `StartRollingDialogs`.
- The target edit region is the tail of `GameConfigViewModel.PlayGameWithSave` (~lines 307-325), currently: a silent `if (!result.Success) { //TODO }`, then an unconditional `Process.GetCurrentProcess().Kill(); //HACK`, then dead cleanup (`save.ReadFromXml(true)`, `GameConfigs` reorder, timestamps, `Saves` reorder, `SelectedSave`, `HubData.Instance.Save()`).
- `using System.Reactive;` is already imported in `GameConfigViewModel.cs` (Step 2a).

---

## File Structure

**Create:**
- `src/ReCap.Hub/ViewModels/Dialog/OkDialogViewModel.cs` — single-button informational dialog VM.
- `src/ReCap.Hub/Views/Dialog/OkDialogView.axaml` (+ `.axaml.cs`) — its view (one "OK" button).

**Modify:**
- `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs` — failure branch shows the dialog + returns (no kill); success path keeps the kill and calls the new `ApplyPostGameSessionState`; extract that method.

**Delete:** none.

---

## Task 1: `OkDialogViewModel` + `OkDialogView`

A single-button dialog mirroring `YesNoDialogViewModel`/`YesNoDialogView`. No unit test — it is a trivial passthrough over `MessageDialogViewModelBase<bool>`; verified by build (Task 3 also renders it via the failure path manually).

**Files:**
- Create: `src/ReCap.Hub/ViewModels/Dialog/OkDialogViewModel.cs`
- Create: `src/ReCap.Hub/Views/Dialog/OkDialogView.axaml`
- Create: `src/ReCap.Hub/Views/Dialog/OkDialogView.axaml.cs`

- [ ] **Step 1: Write `OkDialogViewModel.cs`**

```csharp
using System.Threading.Tasks;

namespace ReCap.Hub.ViewModels
{
    public class OkDialogViewModel
        : MessageDialogViewModelBase<bool>
    {
        public void OkCommand(object parameter)
            => CompletionSource.TrySetResult(true);

        public OkDialogViewModel(string title, string content, bool isCloseable = true)
            : base(title, content, isCloseable)
        { }
    }
}
```

- [ ] **Step 2: Write `OkDialogView.axaml`**

(Copy of `YesNoDialogView.axaml` with a single OK button bound to `OkCommand`; same `DialogRoot`/`DialogFooter`/`DialogBody` themes and `Title`/`Content` bindings.)

```xml
<UserControl x:Class="ReCap.Hub.Views.OkDialogView"
            xmlns="https://github.com/avaloniaui"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:vm="using:ReCap.Hub.ViewModels"
            Theme="{DynamicResource DialogRoot}"
            Width="400">
    <DockPanel>
        <ContentControl Theme="{DynamicResource DialogFooter}"
                        HorizontalContentAlignment="Stretch"
                        DockPanel.Dock="Bottom">
            <DockPanel LastChildFill="False">
                <Button Command="{Binding OkCommand}"
                        Classes="right"
                        DockPanel.Dock="Right">OK</Button>
            </DockPanel>
        </ContentControl>
        <HeaderedContentControl Header="{Binding Title, Mode=OneWay}"
                                Theme="{DynamicResource DialogBody}">
            <StackPanel Orientation="Vertical">
                <TextBlock Text="{Binding Content, Mode=OneWay}" VerticalAlignment="Center"/>
            </StackPanel>
        </HeaderedContentControl>
    </DockPanel>
</UserControl>
```

- [ ] **Step 3: Write `OkDialogView.axaml.cs`**

(Identical pattern to `YesNoDialogView.axaml.cs`.)

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReCap.Hub.Views
{
    public partial class OkDialogView : UserControl
    {
        public OkDialogView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/ViewModels/Dialog/OkDialogViewModel.cs src/ReCap.Hub/Views/Dialog/OkDialogView.axaml src/ReCap.Hub/Views/Dialog/OkDialogView.axaml.cs
git commit -m "feat(ui): add single-button OkDialog for informational/error messages"
```

---

## Task 2: Surface the error + extract post-game cleanup in `GameConfigViewModel`

The success path stays byte-identical (kill, then the same cleanup statements — now inside `ApplyPostGameSessionState`, still executed only after the kill so still dead). The failure path changes: show the OK dialog and `return` without killing.

**Files:**
- Modify: `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs`

- [ ] **Step 1: Replace the tail of `PlayGameWithSave`**

Find this exact region (the current tail, after the `await session.PlayAsync(...)` call assigns `result`):

```csharp
            if (!result.Success)
            {
                //TODO (Step 2b): surface result.Error to the user
            }
            Process.GetCurrentProcess().Kill(); //HACK
            save.ReadFromXml(true);
            HubData.Instance.GameConfigs.Remove(this);
            HubData.Instance.GameConfigs.Insert(0, this);
            //TODO: HubData.Instance.SelectedGameConfig = this;
            _lastLaunchTime = now;
            save.LastLaunchTime = now;
            //save.ReadFromXml()



            Saves.Remove(save);
            Saves.Insert(0, save);
            SelectedSave = save;
            HubData.Instance.Save();
        }
```

Replace it with:

```csharp
            if (!result.Success)
            {
                string message = result.Error?.Message ?? "The game failed to launch.";
                await DialogDisplay.ShowDialog(new OkDialogViewModel("Launch failed", message, true));
                return; // Hub stays alive so the user can see the error — no kill on failure.
            }

            Process.GetCurrentProcess().Kill(); //HACK  (success path unchanged; removed in full Step 2b)
            ApplyPostGameSessionState(save, now);
        }

        private void ApplyPostGameSessionState(SaveGameViewModel save, double now)
        {
            save.ReadFromXml(true);
            HubData.Instance.GameConfigs.Remove(this);
            HubData.Instance.GameConfigs.Insert(0, this);
            //TODO: HubData.Instance.SelectedGameConfig = this;
            _lastLaunchTime = now;
            save.LastLaunchTime = now;
            //save.ReadFromXml()

            Saves.Remove(save);
            Saves.Insert(0, save);
            SelectedSave = save;
            HubData.Instance.Save();
        }
```

Note: `OkDialogViewModel` is in `ReCap.Hub.ViewModels`, the same namespace as `GameConfigViewModel` — no new `using` needed. `DialogDisplay` is in `ReCap.Hub` (already resolvable; it is used elsewhere in this file, e.g. the Locate dialog).

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs
git commit -m "feat(vm): surface launch failure in OkDialog (no kill on failure); extract ApplyPostGameSessionState"
```

---

## Task 3: Final green — both builds, full tests, smoke run

**Files:** none (verification only).

- [ ] **Step 1: Build both configurations**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline` → 0 errors.
Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Release` → 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False`
Expected: 20/20 pass, no regressions (this subset adds no tests; it must not break existing ones).

- [ ] **Step 3: Smoke-run the app** (startup only)

Run the built DLL with an 8s watchdog (PowerShell):
`$p = Start-Process -FilePath "dotnet" -ArgumentList "src/ReCap.Hub/bin/Debug_Offline/net10.0/ResurrectionCapsuleHub.dll" -PassThru; Start-Sleep -Seconds 8; if (!$p.HasExited) { $p.Kill(); "STARTED_OK" } else { "EXITED_EARLY code=$($p.ExitCode)" }`
Expected: `STARTED_OK`. If `EXITED_EARLY` non-zero, capture output and report (likely the new XAML view failed to load / ViewLocator resolution).

- [ ] **Step 4: (Optional) Manual error-path check — no game server required**

Temporarily point a game config's install path at a non-existent directory (or otherwise force `result.Success == false`), click Play, and confirm: the "Launch failed" OK dialog appears and the Hub process does **not** exit. Revert any temporary change. (This is the only validation of the new dialog; the success-path kill/reshow is deferred to full Step 2b and needs a real Darkspore run.)

- [ ] **Step 5: (No commit — verification task.)** If everything is green, the safe subset of Step 2b is done.

---

## Self-Review (against the approved spec)

- **New `OkDialogViewModel` + `OkDialogView`** → Task 1 (namespaces match the ViewLocator convention; single OK button). ✅
- **Failure branch: show error, no kill, Hub survives** → Task 2 Step 1 (`if (!result.Success) { ShowDialog; return; }`). ✅
- **Success path byte-identical (kill stays)** → Task 2 Step 1 keeps `Process.GetCurrentProcess().Kill() //HACK` then calls `ApplyPostGameSessionState` (the same statements, still after the kill → still dead). ✅
- **Extract `ApplyPostGameSessionState(save, now)`** → Task 2 Step 1. ✅
- **No new unit tests (justified); verify by build + suite + smoke + manual error path** → Tasks 1/3. ✅
- **No `.axaml` edits beyond the new view; convention-based resolution** → confirmed in Key Facts. ✅
- **Known caveat (hidden-window dialog visibility on failure) deferred** → documented in spec; Task 3 Step 4 is best-effort manual check. ✅

**Deferred to full Step 2b (needs a real Darkspore run):** remove the kill hack; wire `ApplyPostGameSessionState` to run on success (before/instead of the kill); fix the window hide/reshow lifetime; confirm the failure dialog renders with the window hidden.
