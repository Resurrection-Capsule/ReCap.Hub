# ReCap.Hub modernization — design

**Date:** 2026-06-08
**Status:** approved (brainstorming) → ready for implementation plan
**Scope:** ReCap.Hub internals (architecture, robustness, build). Legacy decoupling deferred (see §8).

## 1. Context

The Hub (`ReCap.Hub`, Avalonia MVVM desktop launcher) works for the offline local-play
path but its internals are hard to maintain — the maintainer calls them "kind of a disaster"
and a 3-agent maintainability analysis (2026-06-08) confirmed a consistent root cause:

**There is no architecture.** Cross-cutting state is reached through static singletons
(`HubData.Instance`, `LocalServer.Instance`, and fully-static `GameLaunchService` / `Patcher`
/ `HubGlobalPaths` / `DialogDisplay`); nothing has an interface, so nothing is mockable or
unit-testable. ViewModels are god-objects (`GameConfigViewModel` ≈ 450 lines orchestrating
patch/launch/server-lifecycle/save-CRUD/XML/global-mutation). There is no domain layer —
business rules are scattered across VMs, services, models, and `App.axaml.cs`. This is wrapped
in over-engineered build machinery (4 solution configs, `#if RECAP_ONLINE`, a half-finished
net5.0 `ReCap.Builder`) and coupled to a legacy stack (binary exe-patching to a hardcoded
127.0.0.1, an embedded ~9.5 MB C++ server).

Goal: make the Hub **robust, maintainable, and modern** by **incrementally strangling** the
chaos layer by layer, keeping the app green and runnable at every step. (Strategy chosen by the
maintainer over a big-bang rewrite and over minimal triage.)

### Already done (prerequisites, on branch `develop`)
- TFM `net5.0` → `net10.0` (net5 EOL; runtime absent). [PR #1]
- Avalonia runtime-binding crash fixed: 30 CommonUI `.axaml` `using:` → `clr-namespace:…;assembly=ReCap.CommonUI` (runtime type resolution). [PR #1]
- `LocateDarksporeView` Accept/Cancel wired + `Accept()` resolves the install root from a manually-entered exe. [committed local]

## 2. Goals & non-goals

**In scope (this design):** Steps 0–6 below — DI + service interfaces, a domain layer, thin
ViewModels, a real test project, robustness fixes, and build simplification.

**Out of scope / deferred:**
- **Step 7 — legacy decoupling** (Patcher → EAWebKit `-recapServer`, embedded C++ server →
  launch the C# `ReCap.Server` by path, vendored CommonUI → NuGet package, login.prop / hardcoded
  URLs → config). Distinct workstream, runs later/parallel (see §8).
- **The views (`.axaml`)** are the maintainer's (Split). We touch them minimally and only where a
  binding must change because its VM contract changed.
- New features (Online Play is a stub; we do not build it out here).

**Success criteria:** ViewModels become unit-testable in isolation; a crash mid-save can no longer
brick the config; the `Process.Kill()` self-termination is gone; adding a screen/feature touches a
small, well-bounded set of files; the build is one `Debug`/`Release` pair with a green CI gate.

## 3. Target architecture

Layers, with dependencies pointing inward:

```
Views (.axaml)              — Split's; minimal edits
   ↓ binding
ViewModels                  — THIN: bind + delegate. No I/O, no Process, no global state
   ↓ constructor injection
Services (interfaces)       — IHubConfigStore, IGameSession, IDarksporeLocator,
   ↓                           IDialogService, ILocalServer, IGameLauncher, IFileSystem, …
Domain (POCOs + rules)      — HubConfig, GameInstall, SaveGame, Hero, Squad — pure data, no UI, no singleton
```

**Pillars:**
- **Real DI** (`Microsoft.Extensions.DependencyInjection`) with a composition root in
  `Program.Main`. The `*.Instance` statics are retired; everything is constructor-injected.
- **Thin ViewModels.** Commands become `ReactiveCommand` (ReactiveUI is already referenced via
  `Avalonia.ReactiveUI`); their `ThrownExceptions` are observed, so command errors surface instead
  of being swallowed by `async void`.
- **Config as data.** `HubData` (VM + singleton + repository) splits into `HubConfig` (POCO) +
  `IHubConfigStore` (atomic load/save + recovery).
- **Real tests.** A new xUnit project (`ReCap.Hub.Tests`, matching the org standard) covers the
  domain and services, which are now mockable.
- **Runtime feature flags** replace `#if RECAP_ONLINE` and the build-mode machinery.

**Principle:** the goal is not to rewrite — it is to strangle the chaos by layer. Where the
existing pattern is sound, follow it.

## 4. Incremental sequence (each step ships green and runnable)

| # | Step | Delivers | Why here |
|---|------|----------|----------|
| 0 | **Foundation** | DI container + composition root in `Program.Main`; xUnit `ReCap.Hub.Tests`; `IFileSystem` + base seams. Existing statics bridge to DI temporarily. No behavior change. | Scaffolding that unblocks everything. |
| 1 | **`HubData` → `HubConfig` + `IHubConfigStore`** | POCO + store with atomic write + recovery (kills corruption + dup-append); removes `ViewModelBase` from the data layer. First real tests. | The central coupling knot; highest leverage. |
| 2 | **`GameConfigViewModel` → `IGameSession`** | Orchestration leaves the VM; kills the `Process.Kill()` HACK; `async void` → `ReactiveCommand`. VM becomes thin. | Worst god-object; most damaging line. |
| 3 | **Locate → `IDarksporeLocator`** | Timer + OS introspection + process-kill leave the dialog VM. Tests for install-path resolution (incl. the manual-exe fix). | Second god-object. |
| 4 | **Dialogs/nav → `IDialogService`** | Replaces static `DialogDisplay` (removes the static event bus + thread races). | Removes dangerous global state. |
| 5 | **Robustness sweep** | Atomic writes everywhere (incl. `AccountModel`), UI-thread marshaling, dead-code removal (InitialDialogWindow, TitledViewModelBase, `#if NO`, backtick files), wire `UnhandledException` → FatalError reporter (and fix its null-deref). | Cleanup once seams exist. |
| 6 | **Build simplification** | 4 configs → `Debug`+`Release` + runtime `FeatureFlags` (removes `#if RECAP_ONLINE` / `EnableReCapOnline` / `EnsureReCapHubFeatures`); fix `.props` `Sdk=` (MSB4011); retire `ReCap.Builder` + publish scripts → publish profiles; bump `ReCap.UITest` → net10; add CI via the org reusable `dotnet-ci`. | Hub gets a green build gate. |
| 7 | **Legacy workstream** — *deferred, see §8* | — | Functional change; separate track. |

Each step is a small PR onto `develop`, independently reviewable.

## 5. Step 1 detail — `HubData` → `HubConfig` + `IHubConfigStore`

**Today:** `HubData : ViewModelBase` with `static Instance`; reads/writes `config.xml` in its
constructor; constructs `GameConfigViewModel`s inside itself (inverted dependency); `Save()` is
called from ~12 sites; writes are non-atomic; the user-prefs elements are appended (not replaced)
on every save (the dup-append bug); no recovery on a corrupt file; default username hardcoded to
`"Splitwirez"`.

**Target — three single-purpose pieces:**
- **`HubConfig`** — plain POCO (no INPC, no singleton): `IReadOnlyList<GameInstall>` (path,
  winePrefix, wineExecutable), `UserDisplayName`, `UseManagedDecorations`, `AutoCloseServer`.
- **`IHubConfigStore`** — `HubConfig Load()` / `void Save(HubConfig)`:
  - **Atomic save:** serialize the whole POCO to a temp file, then `File.Replace`/rename →
    eliminates partial writes and the dup-append (no incremental `XElement.Add`).
  - **Recovering load:** on parse failure, back up the corrupt file and return defaults so the app
    never bricks on a bad config.
  - Built on `IFileSystem` (Step 0) → testable without touching disk.
- **Data no longer knows about ViewModels:** the store returns `HubConfig`; a presentation-level VM
  (LocalPlay) maps config → VMs. The inversion is gone.

**Strangle (stay green):** introduce `HubConfig` + injected `IHubConfigStore`; migrate the ~12
`HubData.Instance.Save()` call sites to it; keep `HubData` as a thin facade during the transition,
then delete it.

**Tests:** round-trip equality; atomic write (no partial file on simulated failure); corrupt-XML →
defaults + backup; no dup-append after N saves; missing file → defaults.

**Bugs retired:** crash-corruption, dup-append, data→VM inversion, hardcoded default username.

## 6. Step 2 detail — `GameConfigViewModel` → `IGameSession`

**Today:** ~450 lines doing path validation, `login.prop` writing, exe patching, server start/stop,
process launch, wait-for-exit, post-exit cleanup (dead code because of the kill), save CRUD, XML
serialization, global-collection mutation, a `static _lastServerInstance`, the
`Process.GetCurrentProcess().Kill() //HACK`, and `async void` commands.

**Target:**
- **`IGameSession`** — `Task<SessionResult> PlayAsync(GameConfig, SaveGame, CancellationToken)`.
  Internally coordinates injected `IPatcher`/`IServerRedirect` + `ILocalServer` + `IGameLauncher`.
  Returns a `SessionResult` (success/failure) instead of killing the process.
- **Kill the HACK:** the session completes, control returns to the Hub cleanly, and the post-game
  cleanup (update save, reorder configs, timestamps) actually runs (it was dead code). The
  window-reshow flow is fixed — that broken reshow was the original reason for the kill.
- **Commands → `ReactiveCommand`:** `ThrownExceptions` is observed → errors surface (testable, not
  swallowed).
- **Thin VM:** `GameConfigViewModel` holds bound state + delegates Play to `IGameSession` and reacts
  to the result. Save CRUD moves to the store / a small service. No `static _lastServerInstance`
  (the server handle is owned by `ILocalServer`).

**Strangler discipline (key):** `IGameSession` here **wraps the existing legacy `Patcher`/
`LocalServer` behind the interface** — behavior does not change yet. The legacy → EAWebKit /
C#-server swap is **Step 7 (deferred)**: we create the seam now and replace the implementation
later. This is exactly why the ordering works.

**Tests:** session orchestration with mocked launcher/server/patcher — correct order (patch → start
server → launch → on exit: stop server if AutoClose, then save); failure paths (server fails to
start → do not launch, surface the error); assert no self-kill.

## 7. Cross-cutting principles

- **Always green:** every step builds in `Debug_Offline`/`Release` and runs; merged as a small PR
  to `develop`.
- **TDD on extracted logic:** when logic moves from a VM/static into a service, its tests are
  written against the new seam as part of the same step.
- **Follow existing sound patterns;** do not refactor unrelated code. Scope each PR to one step.
- **Minimal view edits:** only when a VM contract a binding depends on changes.

## 8. Deferred — Step 7 (legacy decoupling)

Tracked separately; ties into the org modernization already done:
- `IServerRedirect`: `Patcher` (127.0.0.1 byte-patch, needs admin) → launch `Darkspore.exe`
  with `-recapServer=<host[:port]>` (the EAWebKit DLL contract).
- Embedded C++ `recap_server.exe` (~9.5 MB) → launch the C# `ReCap.Server` by path.
- Vendored `src/ReCap.CommonUI` → consume the `ReCap.CommonUI` NuGet package (PR #3 merged; needs a
  `vX.Y.Z` tag to publish). **Note:** PR #3 was packaging only — the `using:` → `clr-namespace`
  runtime-binding fix found here must also be applied to the standalone repo before the Hub consumes
  the package, or the startup crash returns.
- `login.prop` and the hardcoded `127.0.0.1` PNG URL → config-driven.

## 9. Verification

- Per step: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline` (and `-c Release`) →
  0 errors; the app runs (window opens, Locate-Darkspore → lobby).
- `dotnet test` (new `ReCap.Hub.Tests`) green; coverage grows per step (config store, game session,
  locator).
- After Step 6: a CI run on `develop` is green via the reusable `dotnet-ci` workflow.

## 10. Risks

- **~12 `HubData.Instance` call sites** (Step 1) — the facade-during-transition mitigates breakage.
- **Window-reshow flow** (Step 2) — the real reason for the kill hack; needs the actual Avalonia
  lifetime flow understood, not just the kill removed.
- **ReactiveUI command semantics** — `ThrownExceptions` must be subscribed or errors are still lost;
  the composition root wires a global observer.
