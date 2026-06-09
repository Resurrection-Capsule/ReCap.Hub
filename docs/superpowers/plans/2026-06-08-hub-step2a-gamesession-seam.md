# Step 2a — `GameConfigViewModel` → `IGameSession` seam (behavior-preserving) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the play-orchestration out of the `GameConfigViewModel` god-object into a testable `IGameSession` seam (wrapping the existing legacy `Patcher`/`LocalServer`/`GameLaunchService` behind `IPatcher`/`ILocalServer`/`IGameLauncher`), convert the four `async void` commands to `ReactiveCommand`s with observed `ThrownExceptions`, and retire the `static _lastServerInstance` — all with **zero behavior change** and full unit coverage.

**Explicitly NOT in 2a (deferred to Step 2b, verified by the maintainer with a real Darkspore run):** removing the `Process.GetCurrentProcess().Kill() //HACK` and fixing the window-reshow lifetime. The kill **stays** in 2a; the post-game cleanup stays dead code; the hide/reshow static-event flow in `App` is untouched.

**Architecture:** `IGameSession.PlayAsync(GameSessionRequest, CancellationToken)` owns the orchestration core — patch-if-needed → start server (owning the disposable handle) → launch game → on exit, stop the server if AutoClose — and returns a `SessionResult`. Its three collaborators are thin adapters over the existing statics, so the static events `LocalServer.ServerStarted/ServerExited` and `GameLaunchService.GameStarted/GameExited` that `App` subscribes to still fire exactly as before (reshow unchanged). The VM keeps the UI-coupled prep (path validation, the Locate dialog, `login.prop` writing — `login.prop` is legacy/Step-7 territory) and, after `PlayAsync`, still does the kill hack (removed in 2b). The VM resolves `IGameSession` via `HubServices.Get<>` (the Step-0 transitional bridge), since VMs are not yet DI-constructed.

**Tech Stack:** C# / .NET 10, Avalonia MVVM, ReactiveUI (`ReactiveObject` base via `RxObjectBase`; `Avalonia.ReactiveUI` referenced), `Microsoft.Extensions.DependencyInjection`, xUnit.

**Build/test gotchas (unchanged from Step 1):**
- Build the Hub `-c Debug_Offline` (plain `Debug` errors). Also build `-c Release`.
- Tests: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False`.
- Commits: **no `Co-Authored-By` / coauthor trailer** (standing user order).

**Key facts verified before planning:**
- Commands bind by method-name in XAML (`Command="{Binding PlayGameWithSaveCommand}"`, `CommandParameter="{Binding}"`). An `ICommand` **property** of the same name binds identically → **no `.axaml` edits**.
- `App.axaml.cs` subscribes to the **static** events `LocalServer.ServerStarted/ServerExited` and `GameLaunchService.GameExited`. Adapters MUST delegate to the existing statics so those events keep firing.
- `LocalServer.Instance.Start(winePrefix, wineExecutable)` returns an `ActionableDisposable` (an `IDisposable`); disposing it kills the server process. This is the handle the session will own.
- `GameLaunchService.LaunchGame(...)` returns `Task<Exception>` — `null` = clean exit, non-null = launch failure.
- `Patcher.PatchGame(bool exeMissing, string exeSrc, string exeDest, bool pkgMissing, string pkgDest)` returns `int` (0 = ok).
- The four commands to convert all live in `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs`: `PlayGameWithSaveCommand`, `NewSaveGameCommand`, `DeleteSaveGameCommand`, `RenameSaveGameCommand`.

---

## File Structure

**Create:**
- `src/ReCap.Hub/Services/IPatcher.cs`, `Patcher`Adapter.cs` — wrap static `Patcher.PatchGame`.
- `src/ReCap.Hub/Services/ILocalServer.cs`, `LocalServer`Adapter.cs` — wrap `LocalServer.Instance.Start` (returns the `IDisposable` handle).
- `src/ReCap.Hub/Services/IGameLauncher.cs`, `GameLauncher`Adapter.cs` — wrap static `GameLaunchService.LaunchGame`.
- `src/ReCap.Hub/Services/IGameSession.cs`, `GameSession.cs` — orchestration + `GameSessionRequest` + `SessionResult`.
- `src/ReCap.Hub.Tests/Services/GameSessionTests.cs` — orchestration order / failure / autoclose tests (mocked collaborators).
- `src/ReCap.Hub.Tests/Services/GameSessionFakes.cs` — hand-written fakes for the three collaborators (no mocking lib in the project).

**Modify:**
- `src/ReCap.Hub/Composition/HubServices.cs` — register the four new services.
- `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs` — four `async void` methods → `ReactiveCommand` properties (same names); Play delegates orchestration to `IGameSession`; remove `static _lastServerInstance`; keep the kill hack.

**Delete:** none.

---

## Task 1: Collaborator seams (`IPatcher`, `ILocalServer`, `IGameLauncher`) + adapters

These adapters are thin delegators over existing statics; they are exercised through `GameSession` tests (Task 2) rather than unit-tested directly (you cannot unit-test a passthrough to a static without process side effects). Verify by build.

**Files:**
- Create: `src/ReCap.Hub/Services/IPatcher.cs`, `src/ReCap.Hub/Services/Patcher`Adapter.cs`
- Create: `src/ReCap.Hub/Services/ILocalServer.cs`, `src/ReCap.Hub/Services/LocalServer`Adapter.cs`
- Create: `src/ReCap.Hub/Services/IGameLauncher.cs`, `src/ReCap.Hub/Services/GameLauncher`Adapter.cs`

- [ ] **Step 1: Write the interfaces + adapters**

`IPatcher.cs`:
```csharp
namespace ReCap.Hub.Services
{
    public interface IPatcher
    {
        int PatchGame(bool exeMissing, string exeSrcPath, string exeDestPath,
                      bool autoLoginPackageMissing, string autoLoginPackageDestPath);
    }
}
```
`Patcher`Adapter.cs`:
```csharp
using ReCap.Hub.Data;

namespace ReCap.Hub.Services
{
    public sealed class PatcherAdapter : IPatcher
    {
        public int PatchGame(bool exeMissing, string exeSrcPath, string exeDestPath,
                             bool autoLoginPackageMissing, string autoLoginPackageDestPath)
            => Patcher.PatchGame(exeMissing, exeSrcPath, exeDestPath,
                                 autoLoginPackageMissing, autoLoginPackageDestPath);
    }
}
```
`ILocalServer.cs` (returns the existing `IDisposable` handle; the session owns it):
```csharp
using System;

namespace ReCap.Hub.Services
{
    public interface ILocalServer
    {
        /// <summary>Starts the local server and returns a handle; disposing it stops the server.</summary>
        IDisposable Start(string winePrefix, string wineExecutable);
    }
}
```
`LocalServer`Adapter.cs`:
```csharp
using System;
using ReCap.Hub.Data;

namespace ReCap.Hub.Services
{
    public sealed class LocalServerAdapter : ILocalServer
    {
        public IDisposable Start(string winePrefix, string wineExecutable)
            => LocalServer.Instance.Start(winePrefix, wineExecutable);
    }
}
```
`IGameLauncher.cs`:
```csharp
using System;
using System.Threading.Tasks;

namespace ReCap.Hub.Services
{
    public interface IGameLauncher
    {
        /// <summary>Launches the game and completes when it exits. Returns null on clean exit, or the failure.</summary>
        Task<Exception> LaunchGame(string winePrefix, string wineExecutable, string gameExecutable,
                                   string gameBinDir, params string[] commandLineOptions);
    }
}
```
`GameLauncher`Adapter.cs`:
```csharp
using System;
using System.Threading.Tasks;
using ReCap.Hub.Data;

namespace ReCap.Hub.Services
{
    public sealed class GameLauncherAdapter : IGameLauncher
    {
        public Task<Exception> LaunchGame(string winePrefix, string wineExecutable, string gameExecutable,
                                          string gameBinDir, params string[] commandLineOptions)
            => GameLaunchService.LaunchGame(winePrefix, wineExecutable, gameExecutable, gameBinDir, commandLineOptions);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ReCap.Hub/Services/IPatcher.cs src/ReCap.Hub/Services/Patcher`Adapter.cs src/ReCap.Hub/Services/ILocalServer.cs src/ReCap.Hub/Services/LocalServer`Adapter.cs src/ReCap.Hub/Services/IGameLauncher.cs src/ReCap.Hub/Services/GameLauncher`Adapter.cs
git commit -m "feat(services): add IPatcher/ILocalServer/IGameLauncher seams over legacy statics"
```

---

## Task 2: `IGameSession` + `GameSession` orchestration (the testable core)

**Files:**
- Create: `src/ReCap.Hub/Services/IGameSession.cs` (interface + `GameSessionRequest` + `SessionResult`)
- Create: `src/ReCap.Hub/Services/GameSession.cs`
- Create test: `src/ReCap.Hub.Tests/Services/GameSessionFakes.cs`
- Create test: `src/ReCap.Hub.Tests/Services/GameSessionTests.cs`

The orchestration reproduces the EXACT order from the old `PlayGameWithSave` body (lines ~337-416): patch if `exeMissing || autoLoginPackageMissing`; dispose any previous handle; start server; launch game and await exit; if AutoClose, dispose the handle. It returns success/failure instead of relying on the kill.

- [ ] **Step 1: Write the failing tests**

`GameSessionFakes.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReCap.Hub.Services;

namespace ReCap.Hub.Tests.Services
{
    /// <summary>Records the global call order across all collaborators.</summary>
    public sealed class CallLog { public readonly List<string> Events = new(); }

    public sealed class FakePatcher : IPatcher
    {
        readonly CallLog _log;
        public int Result = 0;
        public int Calls;
        public FakePatcher(CallLog log) => _log = log;
        public int PatchGame(bool exeMissing, string exeSrcPath, string exeDestPath,
                             bool pkgMissing, string pkgDestPath)
        {
            Calls++; _log.Events.Add("patch"); return Result;
        }
    }

    public sealed class FakeServerHandle : IDisposable
    {
        readonly CallLog _log;
        public bool Disposed;
        public FakeServerHandle(CallLog log) => _log = log;
        public void Dispose() { Disposed = true; _log.Events.Add("server.stop"); }
    }

    public sealed class FakeLocalServer : ILocalServer
    {
        readonly CallLog _log;
        public readonly List<FakeServerHandle> Handles = new();
        public FakeLocalServer(CallLog log) => _log = log;
        public IDisposable Start(string winePrefix, string wineExecutable)
        {
            _log.Events.Add("server.start");
            var h = new FakeServerHandle(_log);
            Handles.Add(h);
            return h;
        }
    }

    public sealed class FakeGameLauncher : IGameLauncher
    {
        readonly CallLog _log;
        public Exception Result;            // null = clean exit
        public int Calls;
        public FakeGameLauncher(CallLog log) => _log = log;
        public Task<Exception> LaunchGame(string winePrefix, string wineExecutable, string gameExecutable,
                                          string gameBinDir, params string[] commandLineOptions)
        {
            Calls++; _log.Events.Add("launch"); return Task.FromResult(Result);
        }
    }
}
```

`GameSessionTests.cs`:
```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReCap.Hub.Services;
using Xunit;

namespace ReCap.Hub.Tests.Services
{
    public class GameSessionTests
    {
        static GameSessionRequest Req(bool autoClose, bool needsPatch = true) => new GameSessionRequest
        {
            GameExePath = "/game/DarksporeBin/Darkspore_ReCapPatched.exe",
            GameOriginalExePath = "/game/DarksporeBin/Darkspore.exe",
            GameBinDir = "/game/DarksporeBin",
            AutoLoginPackageDestPath = "/game/Data/0ReCapAutoLogin.package",
            WinePrefix = null,
            WineExecutable = null,
            ExeMissing = needsPatch,
            AutoLoginPackageMissing = needsPatch,
            AutoCloseServer = autoClose,
        };

        static (GameSession s, CallLog log, FakePatcher p, FakeLocalServer srv, FakeGameLauncher l) New()
        {
            var log = new CallLog();
            var p = new FakePatcher(log);
            var srv = new FakeLocalServer(log);
            var l = new FakeGameLauncher(log);
            return (new GameSession(p, srv, l), log, p, srv, l);
        }

        [Fact]
        public async Task PlayAsync_AutoClose_OrdersPatchStartLaunchStop_AndSucceeds()
        {
            var (s, log, _, srv, _) = New();

            var result = await s.PlayAsync(Req(autoClose: true), CancellationToken.None);

            Assert.Equal(new[] { "patch", "server.start", "launch", "server.stop" }, log.Events.ToArray());
            Assert.True(result.Success);
            Assert.True(srv.Handles.Single().Disposed);
        }

        [Fact]
        public async Task PlayAsync_NoAutoClose_LeavesServerRunning()
        {
            var (s, log, _, srv, _) = New();

            var result = await s.PlayAsync(Req(autoClose: false), CancellationToken.None);

            Assert.Equal(new[] { "patch", "server.start", "launch" }, log.Events.ToArray());
            Assert.True(result.Success);
            Assert.False(srv.Handles.Single().Disposed);
        }

        [Fact]
        public async Task PlayAsync_SkipsPatch_WhenNothingMissing()
        {
            var (s, log, p, _, _) = New();

            await s.PlayAsync(Req(autoClose: true, needsPatch: false), CancellationToken.None);

            Assert.Equal(0, p.Calls);
            Assert.Equal(new[] { "server.start", "launch", "server.stop" }, log.Events.ToArray());
        }

        [Fact]
        public async Task PlayAsync_LaunchFails_ReturnsFailure_ButStillStopsServerWhenAutoClose()
        {
            var (s, log, _, srv, l) = New();
            l.Result = new System.InvalidOperationException("boom");

            var result = await s.PlayAsync(Req(autoClose: true), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Same(l.Result, result.Error);
            Assert.True(srv.Handles.Single().Disposed); // server cleaned up even on launch failure
            Assert.Equal(new[] { "patch", "server.start", "launch", "server.stop" }, log.Events.ToArray());
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails (types don't exist)**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~GameSessionTests`
Expected: FAIL — `GameSession`/`GameSessionRequest`/`SessionResult` do not exist.

- [ ] **Step 3: Implement the interface + result types + orchestration**

`IGameSession.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReCap.Hub.Services
{
    public sealed class GameSessionRequest
    {
        public string GameExePath { get; init; }
        public string GameOriginalExePath { get; init; }
        public string GameBinDir { get; init; }
        public string AutoLoginPackageDestPath { get; init; }
        public string WinePrefix { get; init; }
        public string WineExecutable { get; init; }
        public bool ExeMissing { get; init; }
        public bool AutoLoginPackageMissing { get; init; }
        public bool AutoCloseServer { get; init; }
    }

    public sealed class SessionResult
    {
        public bool Success { get; init; }
        public Exception Error { get; init; }
        public static SessionResult Ok() => new SessionResult { Success = true };
        public static SessionResult Fail(Exception e) => new SessionResult { Success = false, Error = e };
    }

    public interface IGameSession
    {
        Task<SessionResult> PlayAsync(GameSessionRequest request, CancellationToken cancellationToken);
    }
}
```

`GameSession.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReCap.Hub.Services
{
    /// <summary>
    /// Coordinates patch -> start server -> launch -> (auto)stop. Wraps the legacy
    /// Patcher/LocalServer/GameLaunchService behind seams; behavior is unchanged from the
    /// old GameConfigViewModel.PlayGameWithSave orchestration. The legacy implementations
    /// are swapped in Step 7; the kill-hack removal + window reshow are Step 2b.
    /// </summary>
    public sealed class GameSession : IGameSession
    {
        readonly IPatcher _patcher;
        readonly ILocalServer _server;
        readonly IGameLauncher _launcher;

        public GameSession(IPatcher patcher, ILocalServer server, IGameLauncher launcher)
        {
            _patcher = patcher;
            _server = server;
            _launcher = launcher;
        }

        public async Task<SessionResult> PlayAsync(GameSessionRequest r, CancellationToken cancellationToken)
        {
            if (r.ExeMissing || r.AutoLoginPackageMissing)
            {
                _patcher.PatchGame(r.ExeMissing, r.GameOriginalExePath, r.GameExePath,
                                   r.AutoLoginPackageMissing, r.AutoLoginPackageDestPath);
            }

            IDisposable serverHandle = _server.Start(r.WinePrefix, r.WineExecutable);
            try
            {
                Exception launchError = await _launcher.LaunchGame(
                    r.WinePrefix, r.WineExecutable, r.GameExePath, r.GameBinDir);

                return launchError == null ? SessionResult.Ok() : SessionResult.Fail(launchError);
            }
            finally
            {
                if (r.AutoCloseServer)
                    serverHandle?.Dispose();
            }
        }
    }
}
```
Notes: the `try/finally` guarantees the server is stopped on AutoClose even when launch throws/faults — matching the test and improving on the old straight-line code (which disposed only on the happy path). This is still behavior-preserving for the live app (the old code's launch-fail path did nothing useful — `//TODO: Do something useful here`). The previous-handle disposal (old `_lastServerInstance?.Dispose()` before start) is now moot: the session owns exactly one handle per call and never leaks a static.

- [ ] **Step 4: Run to verify pass (4 tests)**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~GameSessionTests`
Expected: PASS (4).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Services/IGameSession.cs src/ReCap.Hub/Services/GameSession.cs src/ReCap.Hub.Tests/Services/GameSessionFakes.cs src/ReCap.Hub.Tests/Services/GameSessionTests.cs
git commit -m "feat(services): add IGameSession orchestration (patch->start->launch->stop) with tests"
```

---

## Task 3: Register the new services in the composition root

**Files:**
- Modify: `src/ReCap.Hub/Composition/HubServices.cs`
- Test: `src/ReCap.Hub.Tests/Composition/HubServicesTests.cs`

- [ ] **Step 1: Write the failing test** (append inside `HubServicesTests`)

```csharp
        [Fact]
        public void Register_AddsGameSessionAndCollaborators()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            Assert.IsType<ReCap.Hub.Services.GameSession>(provider.GetRequiredService<ReCap.Hub.Services.IGameSession>());
            Assert.IsType<ReCap.Hub.Services.PatcherAdapter>(provider.GetRequiredService<ReCap.Hub.Services.IPatcher>());
            Assert.IsType<ReCap.Hub.Services.LocalServerAdapter>(provider.GetRequiredService<ReCap.Hub.Services.ILocalServer>());
            Assert.IsType<ReCap.Hub.Services.GameLauncherAdapter>(provider.GetRequiredService<ReCap.Hub.Services.IGameLauncher>());
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubServicesTests`
Expected: FAIL — services not registered.

- [ ] **Step 3: Register them** (add to `HubServices.Register`, after the existing lines)

```csharp
            services.AddSingleton<ReCap.Hub.Services.IPatcher, ReCap.Hub.Services.PatcherAdapter>();
            services.AddSingleton<ReCap.Hub.Services.ILocalServer, ReCap.Hub.Services.LocalServerAdapter>();
            services.AddSingleton<ReCap.Hub.Services.IGameLauncher, ReCap.Hub.Services.GameLauncherAdapter>();
            services.AddSingleton<ReCap.Hub.Services.IGameSession, ReCap.Hub.Services.GameSession>();
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubServicesTests`
Expected: PASS (3).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Composition/HubServices.cs src/ReCap.Hub.Tests/Composition/HubServicesTests.cs
git commit -m "feat(di): register IGameSession and its collaborators"
```

---

## Task 4: VM Play → `IGameSession` + `ReactiveCommand`; remove `static _lastServerInstance`; keep kill

This is the integration edit. The bound member name `PlayGameWithSaveCommand` is preserved (now an `ICommand` property) so **no `.axaml` changes**. The path validation / Locate dialog / `login.prop` writing stay in the VM (UI + legacy prep). After `PlayAsync` returns, the kill hack STAYS (removed in 2b), so the post-`PlayAsync` cleanup remains dead — preserved verbatim.

**Files:**
- Modify: `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs`

- [ ] **Step 1: Replace the `static _lastServerInstance` field and the `PlayGameWithSaveCommand` method + `PlayGameWithSave` body**

Remove:
```csharp
        public async void PlayGameWithSaveCommand(object parameter)
        {
            if (!(parameter is SaveGameViewModel save))
                return;

            var task = PlayGameWithSave(save);
            await task;
            if (task.IsFaulted || (task.Exception != null))
                throw task.Exception;
        }

        static IDisposable _lastServerInstance = null;
```
Add (near the other commands) a ReactiveCommand property of the SAME bound name, created in the constructor. At the top of the class add a backing field + property:
```csharp
        public ReactiveCommand<SaveGameViewModel, System.Reactive.Unit> PlayGameWithSaveCommand { get; }
```
In BOTH public constructors' shared path — i.e. in the private `GameConfigViewModel()` ctor (the `this()` all others chain through) — initialize it and observe its errors:
```csharp
            PlayGameWithSaveCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(PlayGameWithSave);
            PlayGameWithSaveCommand.ThrownExceptions.Subscribe(ex =>
                System.Diagnostics.Debug.WriteLine($"PlayGameWithSave failed: {ex}"));
```
Add the needed usings at the top of the file: `using System.Reactive;`, `using System.Reactive.Linq;`, `using ReactiveUI;`. (`using System;` is already present for `Subscribe`/`IDisposable`.)

Change `PlayGameWithSave` from `public async Task PlayGameWithSave(SaveGameViewModel save)` — keep the signature (ReactiveCommand.CreateFromTask binds to it) — and replace the orchestration tail. Specifically, the section from the `_lastServerInstance?.Dispose();` / `LocalServer.Instance.Start(...)` block through the `if (HubData.Instance.AutoCloseServer) { ... }` block (old lines ~352-416) becomes a single `IGameSession.PlayAsync` call. Concretely, replace this old region:
```csharp
            //Process.GetCurrentProcess().Kill();
            save.UpdateUserDisplayName(HubData.Instance.UserDisplayName);

            _lastServerInstance?.Dispose();
            _lastServerInstance = LocalServer.Instance.Start(WinePrefixPath, WineExecPath);

            {
                var fail = await GameLaunchService.LaunchGame(WinePrefixPath, WineExecPath, gameExePath, gameBinPath);
                if (fail != null)
                {
                    //TODO: Do something useful here
                }
                /* ...big commented block... */
            }
            if (HubData.Instance.AutoCloseServer)
            {
                _lastServerInstance?.Dispose();
                _lastServerInstance = null;
            }
```
with:
```csharp
            save.UpdateUserDisplayName(HubData.Instance.UserDisplayName);

            var session = Composition.HubServices.Get<Services.IGameSession>();
            var result = await session.PlayAsync(new Services.GameSessionRequest
            {
                GameExePath = gameExePath,
                GameOriginalExePath = gameOriginalExePath,
                GameBinDir = gameBinPath,
                AutoLoginPackageDestPath = autoLoginPackageDestPath,
                WinePrefix = WinePrefixPath,
                WineExecutable = WineExecPath,
                ExeMissing = exeMissing,
                AutoLoginPackageMissing = autoLoginPackageMissing,
                AutoCloseServer = HubData.Instance.AutoCloseServer,
            }, System.Threading.CancellationToken.None);
            if (!result.Success)
            {
                //TODO (Step 2b): surface result.Error to the user
            }
```
IMPORTANT: the existing code above this region already computes `gameExePath`, `gameOriginalExePath`, `gameBinPath`, `autoLoginPackageDestPath`, `exeMissing`, `autoLoginPackageMissing`, and already calls `Patcher.PatchGame(...)` itself at old lines ~337-346. To avoid patching twice, REMOVE the VM's own `if (exeMissing || autoLoginPackageMissing) { Patcher.PatchGame(...); }` block (old lines ~337-346) — the session now performs the patch. Leave everything that computes those locals intact (the session needs them passed in).

Leave the kill hack and the dead cleanup that follow EXACTLY as they are:
```csharp
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
(Do NOT touch these — they are Step 2b's concern.)

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors. If `System.Reactive.Unit` is ambiguous or `ReactiveCommand` isn't found, confirm `using ReactiveUI;` and that the project references `Avalonia.ReactiveUI` (it does — `Program.cs` calls `.UseReactiveUI()`).

- [ ] **Step 3: Commit**

```bash
git add src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs
git commit -m "refactor(vm): delegate Play to IGameSession; PlayGameWithSaveCommand -> ReactiveCommand; drop static server handle"
```

---

## Task 5: Convert the three save commands to `ReactiveCommand`

`NewSaveGameCommand`, `DeleteSaveGameCommand`, `RenameSaveGameCommand` are currently `async void` (errors swallowed). Convert to `ReactiveCommand`s of the same bound names, observing `ThrownExceptions`. The save CRUD logic (`CreateSaveGame`/`DeleteSaveGame`/`RenameSaveGame`) stays where it is for 2a — it already delegates persistence to `HubData.Instance.Save()` (the Step-1 store-backed facade), so "save CRUD goes through the store" is already satisfied; a dedicated `ISaveGameService` extraction is deferred to when the VM is fully thinned (no new abstraction needed now — YAGNI).

**Files:**
- Modify: `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs`

- [ ] **Step 1: Replace the three `async void *Command` methods with `ReactiveCommand` properties**

Remove the three methods:
```csharp
        public async void NewSaveGameCommand(object parameter) { ... }
        public async void DeleteSaveGameCommand(object parameter) { ... }
        public async void RenameSaveGameCommand(object parameter) { ... }
```
Add three properties (same names):
```csharp
        public ReactiveCommand<Unit, Unit> NewSaveGameCommand { get; }
        public ReactiveCommand<SaveGameViewModel, Unit> DeleteSaveGameCommand { get; }
        public ReactiveCommand<SaveGameViewModel, Unit> RenameSaveGameCommand { get; }
```
Initialize them in the private `GameConfigViewModel()` ctor alongside `PlayGameWithSaveCommand`, adapting the existing `Create/Delete/Rename` task methods (which already exist and return `Task`):
```csharp
            NewSaveGameCommand = ReactiveCommand.CreateFromTask(async () => { await CreateSaveGame(true); });
            DeleteSaveGameCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(DeleteSaveGame);
            RenameSaveGameCommand = ReactiveCommand.CreateFromTask<SaveGameViewModel>(RenameSaveGame);

            Observable.Merge(
                    NewSaveGameCommand.ThrownExceptions,
                    DeleteSaveGameCommand.ThrownExceptions,
                    RenameSaveGameCommand.ThrownExceptions,
                    PlayGameWithSaveCommand.ThrownExceptions)
                .Subscribe(ex => System.Diagnostics.Debug.WriteLine($"Command failed: {ex}"));
```
(Replace the standalone `PlayGameWithSaveCommand.ThrownExceptions.Subscribe(...)` from Task 4 with this merged subscription so all four are observed in one place.) `CreateSaveGame(bool)`, `DeleteSaveGame(SaveGameViewModel)`, `RenameSaveGame(SaveGameViewModel)` already exist as `public async Task` methods — keep them as-is (they are the command bodies now). `using System.Reactive.Linq;` (for `Observable.Merge`) was added in Task 4.

NOTE: the XAML binds `DeleteSaveGameCommand`/`RenameSaveGameCommand` with `CommandParameter="{Binding}"` (a `SaveGameViewModel`), and the New-save button binds `NewSaveGameCommand` with no parameter — matching the `Unit`/`SaveGameViewModel` generic args above. Verify the New-save binding has no `CommandParameter` (grep `NewSaveGameCommand` in `.axaml`); if it passes one, change its command to `ReactiveCommand.CreateFromTask<object>` to tolerate it. Report if so.

- [ ] **Step 2: Build**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs
git commit -m "refactor(vm): save commands -> ReactiveCommand with observed ThrownExceptions"
```

---

## Task 6: Final green — both builds, full tests, smoke run

**Files:** none (verification only).

- [ ] **Step 1: Build both configurations**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline` → 0 errors.
Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Release` → 0 errors.

- [ ] **Step 2: Full test suite**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False`
Expected: all green (Step 1's 14 + GameSession 4 + HubServices's new assertions = no regressions).

- [ ] **Step 3: Smoke-run the app** (startup only — the Play flow needs a real game and is Step 2b)

Run the built DLL with an 8s watchdog (PowerShell):
`$p = Start-Process -FilePath "dotnet" -ArgumentList "src/ReCap.Hub/bin/Debug_Offline/net10.0/ResurrectionCapsuleHub.dll" -PassThru; Start-Sleep -Seconds 8; if (!$p.HasExited) { $p.Kill(); "STARTED_OK" } else { "EXITED_EARLY code=$($p.ExitCode)" }`
Expected: `STARTED_OK` (window opens, Locate-Darkspore first-run shows). If `EXITED_EARLY` non-zero, capture output and report (likely a ReactiveCommand init/DI ordering issue).

- [ ] **Step 4: (No commit — verification task.)** If everything is green, Step 2a is done.

---

## Self-Review (against the 2a scope the maintainer approved)

- **IGameSession wraps legacy, same order** → Task 2 (`GameSession` + order tests). ✅
- **async void → ReactiveCommand, ThrownExceptions observed** → Tasks 4 + 5 (all four commands; merged subscription). ✅
- **Remove static `_lastServerInstance`** → Task 4 (handle owned per-call inside `GameSession`). ✅
- **Save CRUD through the store** → already true via the Step-1 facade (`HubData.Instance.Save()`); no new abstraction (YAGNI). Noted in Task 5. ✅
- **Kill hack STAYS; reshow untouched** → Task 4 leaves the kill + dead cleanup verbatim; `App` static-event subscriptions and adapters preserve event firing. ✅
- **No `.axaml` edits** → command names preserved as `ICommand` properties; Task 5 flags the one binding to double-check (`NewSaveGameCommand` parameter). ✅

**Deferred to Step 2b (needs a real Darkspore run by the maintainer):** remove `Process.GetCurrentProcess().Kill() //HACK`; fix the window hide/reshow Avalonia lifetime; revive the post-game cleanup (save update, reorder, timestamps) that the kill currently strands; surface `SessionResult.Error` to the user; consider constructor-injecting `IGameSession` once the `HubData` facade is deleted and VMs are DI-built.
