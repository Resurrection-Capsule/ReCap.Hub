# Step 1 — `HubData` → `HubConfig` + `IHubConfigStore` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the `HubData` god-object (VM + singleton + repository) into a pure `HubConfig` POCO plus an `IHubConfigStore` that loads/saves `config.xml` atomically with corruption recovery, retiring the dup-append, crash-corruption, data→VM-inversion, and hardcoded-username bugs.

**Architecture:** A VM-free domain trio (`HubConfig` / `GameInstall` / `SaveRef`) holds the config as data. `HubConfigStore` (built on the Step 0 `IFileSystem` seam) serializes the *whole* POCO to a temp file then `Move`-replaces the target (atomic, no incremental `XElement.Add` → no dup-append), and on a parse failure backs up the corrupt file and returns defaults. `HubData` is reduced to a thin INPC facade that maps `HubConfig` ↔ ViewModels and delegates all persistence to the injected store, so the ~12 existing `HubData.Instance.Save()` / property call sites keep working unchanged. The facade is slated for deletion in Step 2.

**Tech Stack:** C# / .NET 10, Avalonia MVVM, `System.Xml.Linq`, `Microsoft.Extensions.DependencyInjection`, xUnit.

**Build/test gotchas (from `hub-build-run` memory — critical):**
- Build the Hub with `-c Debug_Offline` (plain `Debug` errors on unset `EnableReCapOnline`).
- Run tests with `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False` (the test project references the Hub, which needs that flag).
- Assembly name is `ResurrectionCapsuleHub`.
- Commits: **no `Co-Authored-By` / coauthor trailer** (user order).

**Decisions locked for this step:**
- Nested `<save>` pointers → typed `SaveRef { Id, LastLaunchTime }` (confirmed with maintainer). The real per-save data (`AccountModel`, separate `<id>.xml` files) is **not** touched — that is Step 2/3.
- Hardcoded default username `"Splitwirez"` is retired → neutral constant `HubConfig.DefaultUserDisplayName = "Player"`, and the store now actually *writes* `userDisplayName` (the old `Save()` never did).
- On-disk schema is preserved byte-equivalently so the app stays green: root `<hub>` → `<gameConfigs>`/`<gameConfig …>` (attrs `gameInstallPath`, `savesPath`, `winePrefix`, `wineExecutable`, `displayName`) with `<save id=".." lastLaunchTime=".."/>` children, plus `<preferences>` with `<userDisplayName>`, `<useManagedWindowDecorations>`, `<autoCloseServer>`.

---

## File Structure

**Create:**
- `src/ReCap.Hub/Domain/HubConfig.cs` — root POCO + `GameInstall` + `SaveRef` + defaults. No INPC, no singleton, no XML knowledge.
- `src/ReCap.Hub/Services/IHubConfigStore.cs` — `HubConfig Load()` / `void Save(HubConfig)`.
- `src/ReCap.Hub/Services/HubConfigStore.cs` — XML serialization + atomic write + recovering load, on `IFileSystem`.
- `src/ReCap.Hub.Tests/Services/FakeFileSystem.cs` — in-memory `IFileSystem` test double.
- `src/ReCap.Hub.Tests/Domain/HubConfigTests.cs` — defaults.
- `src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs` — load/save/atomic/recovery.

**Modify:**
- `src/ReCap.Hub/Composition/HubServices.cs` — register `IHubConfigStore`.
- `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs` — add `GameConfigViewModel(GameInstall)` ctor + `ToGameInstall()`; remove the now-dead `ref XElement` ctor and `WriteToXml`.
- `src/ReCap.Hub/ViewModels/Local/SaveGameViewModel.cs` — add `LastLaunchTime`-bearing `ToSaveRef()`; remove the now-dead `WriteToXml`.
- `src/ReCap.Hub/Data/HubData.cs` — reduce to a thin INPC facade delegating persistence to `IHubConfigStore`; drop `ReadXml`/`_doc`/XML constants it no longer owns.

**Delete:** none (HubData stays as transitional facade; deleted in Step 2).

---

## Task 1: Domain POCOs (`HubConfig`, `GameInstall`, `SaveRef`)

**Files:**
- Create: `src/ReCap.Hub/Domain/HubConfig.cs`
- Test: `src/ReCap.Hub.Tests/Domain/HubConfigTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using ReCap.Hub.Domain;
using Xunit;

namespace ReCap.Hub.Tests.Domain
{
    public class HubConfigTests
    {
        [Fact]
        public void Default_HasNeutralUsernameNoInstallsAndAutoCloseOn()
        {
            var cfg = HubConfig.Default;

            Assert.Equal("Player", cfg.UserDisplayName);
            Assert.Empty(cfg.GameInstalls);
            Assert.True(cfg.AutoCloseServer);
        }

        [Fact]
        public void Default_UserDisplayNameIsNotTheOldHardcodedName()
        {
            Assert.NotEqual("Splitwirez", HubConfig.Default.UserDisplayName);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigTests`
Expected: FAIL — `HubConfig` / `ReCap.Hub.Domain` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Collections.Generic;
using CommonUI.Util;

namespace ReCap.Hub.Domain
{
    /// <summary>Pure config data. No INPC, no singleton, no XML, no ViewModels.</summary>
    public sealed class HubConfig
    {
        public const string DefaultUserDisplayName = "Player";

        public IReadOnlyList<GameInstall> GameInstalls { get; init; } = new List<GameInstall>();
        public string UserDisplayName { get; init; } = DefaultUserDisplayName;
        public bool UseManagedDecorations { get; init; } = OSInfo.IsWindows;
        public bool AutoCloseServer { get; init; } = true;

        public static HubConfig Default => new HubConfig();
    }

    public sealed class GameInstall
    {
        public string GameInstallPath { get; init; } = string.Empty;
        public string SavesPath { get; init; } = string.Empty;
        public string WinePrefix { get; init; }
        public string WineExecutable { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public IReadOnlyList<SaveRef> Saves { get; init; } = new List<SaveRef>();
    }

    public sealed class SaveRef
    {
        public string Id { get; init; } = string.Empty;
        public double LastLaunchTime { get; init; } = -1;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Domain/HubConfig.cs src/ReCap.Hub.Tests/Domain/HubConfigTests.cs
git commit -m "feat(domain): add HubConfig/GameInstall/SaveRef POCOs"
```

---

## Task 2: Test double — `FakeFileSystem`

A disk-free `IFileSystem` so store tests are deterministic and can simulate write failures.

**Files:**
- Create: `src/ReCap.Hub.Tests/Services/FakeFileSystem.cs`

- [ ] **Step 1: Write the implementation** (no separate test — exercised by Task 3+)

```csharp
using System;
using System.Collections.Generic;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Tests.Services
{
    /// <summary>In-memory IFileSystem. Paths are compared ordinally (case-sensitive is fine for tests).</summary>
    public sealed class FakeFileSystem : IFileSystem
    {
        public readonly Dictionary<string, string> Files = new();
        public readonly HashSet<string> Dirs = new();

        /// <summary>If set, WriteAllText throws for any path the predicate returns true for.</summary>
        public Func<string, bool> FailWriteWhen;

        public bool FileExists(string path) => Files.ContainsKey(path);

        public string ReadAllText(string path) => Files[path];

        public void WriteAllText(string path, string contents)
        {
            if (FailWriteWhen != null && FailWriteWhen(path))
                throw new System.IO.IOException("simulated write failure: " + path);
            Files[path] = contents;
        }

        public void Move(string sourcePath, string destPath, bool overwrite)
        {
            if (!Files.ContainsKey(sourcePath))
                throw new System.IO.FileNotFoundException(sourcePath);
            if (Files.ContainsKey(destPath) && !overwrite)
                throw new System.IO.IOException("dest exists: " + destPath);
            Files[destPath] = Files[sourcePath];
            Files.Remove(sourcePath);
        }

        public void Delete(string path) => Files.Remove(path);

        public void CreateDirectory(string path) => Dirs.Add(path);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/ReCap.Hub.Tests/Services/FakeFileSystem.cs
git commit -m "test: add in-memory FakeFileSystem double"
```

---

## Task 3: `IHubConfigStore` + load of a missing file → defaults

**Files:**
- Create: `src/ReCap.Hub/Services/IHubConfigStore.cs`
- Create: `src/ReCap.Hub/Services/HubConfigStore.cs`
- Test: `src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using ReCap.Hub.Domain;
using ReCap.Hub.Services;
using Xunit;

namespace ReCap.Hub.Tests.Services
{
    public class HubConfigStoreTests
    {
        const string CfgDir = "/cfg";
        const string CfgPath = "/cfg/config.xml";

        static HubConfigStore NewStore(FakeFileSystem fs) => new HubConfigStore(fs, CfgDir, CfgPath);

        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var fs = new FakeFileSystem();

            var cfg = NewStore(fs).Load();

            Assert.Equal(HubConfig.DefaultUserDisplayName, cfg.UserDisplayName);
            Assert.Empty(cfg.GameInstalls);
            Assert.True(cfg.AutoCloseServer);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: FAIL — `IHubConfigStore` / `HubConfigStore` do not exist.

- [ ] **Step 3: Write minimal implementation**

`src/ReCap.Hub/Services/IHubConfigStore.cs`:

```csharp
using ReCap.Hub.Domain;

namespace ReCap.Hub.Services
{
    public interface IHubConfigStore
    {
        HubConfig Load();
        void Save(HubConfig config);
    }
}
```

`src/ReCap.Hub/Services/HubConfigStore.cs` (full file — `Save` filled in Task 4, recovery in Task 5):

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ReCap.Hub.Domain;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Services
{
    public sealed class HubConfigStore : IHubConfigStore
    {
        const string ROOT_EL = "hub";
        const string GAME_CONFIGS_EL = "gameConfigs";
        const string GAME_CONFIG_EL = "gameConfig";
        const string GAME_PATH_ATTR = "gameInstallPath";
        const string SAVES_PATH_ATTR = "savesPath";
        const string WINE_PFX_ATTR = "winePrefix";
        const string WINE_EX_ATTR = "wineExecutable";
        const string DISPLAY_NAME_ATTR = "displayName";
        const string SAVE_EL = "save";
        const string SAVE_ID_ATTR = "id";
        const string SAVE_LLT_ATTR = "lastLaunchTime";
        const string USER_PREFS_EL = "preferences";
        const string USER_DISPLAY_NAME_EL = "userDisplayName";
        const string USE_MANAGED_DECORATIONS_EL = "useManagedWindowDecorations";
        const string AUTO_CLOSE_SERVER_EL = "autoCloseServer";

        readonly IFileSystem _fs;
        readonly string _cfgDir;
        readonly string _cfgPath;

        public HubConfigStore(IFileSystem fs, string configDir, string configPath)
        {
            _fs = fs;
            _cfgDir = configDir;
            _cfgPath = configPath;
        }

        public HubConfig Load()
        {
            if (!_fs.FileExists(_cfgPath))
                return HubConfig.Default;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(_fs.ReadAllText(_cfgPath));
            }
            catch (System.Xml.XmlException)
            {
                BackUpCorruptFile();
                return HubConfig.Default;
            }

            return Parse(doc);
        }

        public void Save(HubConfig config)
        {
            // Filled in Task 4.
            throw new NotImplementedException();
        }

        HubConfig Parse(XDocument doc)
        {
            var root = doc.Root;
            var installs = new List<GameInstall>();

            var gameConfigsEl = root?.Element(GAME_CONFIGS_EL);
            if (gameConfigsEl != null)
            {
                foreach (var el in gameConfigsEl.Elements(GAME_CONFIG_EL))
                {
                    var saves = el.Elements(SAVE_EL)
                        .Where(s => s.Attribute(SAVE_ID_ATTR) != null)
                        .Select(s => new SaveRef
                        {
                            Id = s.Attribute(SAVE_ID_ATTR).Value,
                            LastLaunchTime = ParseDouble(s.Attribute(SAVE_LLT_ATTR)?.Value),
                        })
                        .ToList();

                    installs.Add(new GameInstall
                    {
                        GameInstallPath = el.Attribute(GAME_PATH_ATTR)?.Value ?? string.Empty,
                        SavesPath = el.Attribute(SAVES_PATH_ATTR)?.Value ?? string.Empty,
                        WinePrefix = el.Attribute(WINE_PFX_ATTR)?.Value,
                        WineExecutable = el.Attribute(WINE_EX_ATTR)?.Value,
                        DisplayName = el.Attribute(DISPLAY_NAME_ATTR)?.Value ?? string.Empty,
                        Saves = saves,
                    });
                }
            }

            var prefsEl = root?.Element(USER_PREFS_EL);
            string displayName = HubConfig.DefaultUserDisplayName;
            bool useManaged = HubConfig.Default.UseManagedDecorations;
            bool autoClose = HubConfig.Default.AutoCloseServer;
            if (prefsEl != null)
            {
                if (prefsEl.Element(USER_DISPLAY_NAME_EL) is XElement dn && !string.IsNullOrEmpty(dn.Value))
                    displayName = dn.Value;
                if (prefsEl.Element(USE_MANAGED_DECORATIONS_EL) is XElement md && bool.TryParse(md.Value, out var mdv))
                    useManaged = mdv;
                if (prefsEl.Element(AUTO_CLOSE_SERVER_EL) is XElement ac && bool.TryParse(ac.Value, out var acv))
                    autoClose = acv;
            }

            return new HubConfig
            {
                GameInstalls = installs,
                UserDisplayName = displayName,
                UseManagedDecorations = useManaged,
                AutoCloseServer = autoClose,
            };
        }

        static double ParseDouble(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : -1;

        void BackUpCorruptFile()
        {
            // Filled in Task 5.
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Services/IHubConfigStore.cs src/ReCap.Hub/Services/HubConfigStore.cs src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs
git commit -m "feat(services): add IHubConfigStore + recovering load (missing->defaults)"
```

---

## Task 4: Atomic `Save` + round-trip + no dup-append + atomic-failure

**Files:**
- Modify: `src/ReCap.Hub/Services/HubConfigStore.cs` (implement `Save`)
- Test: `src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs`

- [ ] **Step 1: Write the failing tests** (append inside `HubConfigStoreTests`)

```csharp
        static HubConfig SampleConfig() => new HubConfig
        {
            UserDisplayName = "Ada",
            UseManagedDecorations = false,
            AutoCloseServer = false,
            GameInstalls = new[]
            {
                new GameInstall
                {
                    GameInstallPath = "/games/darkspore",
                    SavesPath = "/saves",
                    WinePrefix = "/wine/pfx",
                    WineExecutable = "/usr/bin/wine",
                    DisplayName = "My Install",
                    Saves = new[]
                    {
                        new SaveRef { Id = "alice", LastLaunchTime = 123.5 },
                        new SaveRef { Id = "bob", LastLaunchTime = 200 },
                    },
                },
            },
        };

        static void AssertConfigsEqual(HubConfig expected, HubConfig actual)
        {
            Assert.Equal(expected.UserDisplayName, actual.UserDisplayName);
            Assert.Equal(expected.UseManagedDecorations, actual.UseManagedDecorations);
            Assert.Equal(expected.AutoCloseServer, actual.AutoCloseServer);
            Assert.Equal(expected.GameInstalls.Count, actual.GameInstalls.Count);
            for (int i = 0; i < expected.GameInstalls.Count; i++)
            {
                var e = expected.GameInstalls[i];
                var a = actual.GameInstalls[i];
                Assert.Equal(e.GameInstallPath, a.GameInstallPath);
                Assert.Equal(e.SavesPath, a.SavesPath);
                Assert.Equal(e.WinePrefix, a.WinePrefix);
                Assert.Equal(e.WineExecutable, a.WineExecutable);
                Assert.Equal(e.DisplayName, a.DisplayName);
                Assert.Equal(e.Saves.Count, a.Saves.Count);
                for (int j = 0; j < e.Saves.Count; j++)
                {
                    Assert.Equal(e.Saves[j].Id, a.Saves[j].Id);
                    Assert.Equal(e.Saves[j].LastLaunchTime, a.Saves[j].LastLaunchTime);
                }
            }
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var fs = new FakeFileSystem();
            var store = NewStore(fs);
            var cfg = SampleConfig();

            store.Save(cfg);
            var loaded = store.Load();

            AssertConfigsEqual(cfg, loaded);
        }

        [Fact]
        public void Save_IsAtomic_WritesViaTempThenMove_NoTempLeftBehind()
        {
            var fs = new FakeFileSystem();
            NewStore(fs).Save(SampleConfig());

            Assert.True(fs.FileExists(CfgPath));
            Assert.DoesNotContain(fs.Files.Keys, k => k != CfgPath && k.StartsWith(CfgPath));
        }

        [Fact]
        public void Save_NSaves_DoNotDuplicatePreferences()
        {
            var fs = new FakeFileSystem();
            var store = NewStore(fs);

            for (int i = 0; i < 5; i++)
                store.Save(SampleConfig());

            var doc = System.Xml.Linq.XDocument.Parse(fs.Files[CfgPath]);
            Assert.Single(doc.Root.Elements("preferences"));
            Assert.Single(doc.Root.Elements("preferences").First().Elements("autoCloseServer"));
            Assert.Single(doc.Root.Elements("gameConfigs"));
            Assert.Single(doc.Root.Elements("gameConfigs").First().Elements("gameConfig"));
        }

        [Fact]
        public void Save_WriteFailsToTemp_LeavesExistingFileUntouched()
        {
            var fs = new FakeFileSystem();
            // Seed an existing good file via a successful save.
            NewStore(fs).Save(SampleConfig());
            string good = fs.Files[CfgPath];

            // Now make any temp write fail and attempt a different save.
            fs.FailWriteWhen = path => path != CfgPath; // temp path differs from target
            var failing = NewStore(fs);

            Assert.ThrowsAny<System.Exception>(() =>
                failing.Save(new HubConfig { UserDisplayName = "should-not-persist" }));

            Assert.Equal(good, fs.Files[CfgPath]); // target unchanged
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: FAIL — `Save` throws `NotImplementedException`.

- [ ] **Step 3: Implement `Save`** (replace the `Save` body and add `Serialize` in `HubConfigStore`)

```csharp
        public void Save(HubConfig config)
        {
            var doc = Serialize(config);

            _fs.CreateDirectory(_cfgDir);

            string tempPath = _cfgPath + ".tmp";
            _fs.WriteAllText(tempPath, doc.ToString());   // whole-document write — no incremental Add
            _fs.Move(tempPath, _cfgPath, overwrite: true); // atomic replace
        }

        XDocument Serialize(HubConfig config)
        {
            var gameConfigsEl = new XElement(GAME_CONFIGS_EL);
            foreach (var install in config.GameInstalls)
            {
                var el = new XElement(GAME_CONFIG_EL);
                el.SetAttributeValue(GAME_PATH_ATTR, install.GameInstallPath);
                el.SetAttributeValue(SAVES_PATH_ATTR, install.SavesPath);
                el.SetAttributeValue(WINE_PFX_ATTR, install.WinePrefix);     // null => attribute omitted
                el.SetAttributeValue(WINE_EX_ATTR, install.WineExecutable);  // null => attribute omitted
                el.SetAttributeValue(DISPLAY_NAME_ATTR, install.DisplayName);
                foreach (var save in install.Saves)
                {
                    var saveEl = new XElement(SAVE_EL);
                    saveEl.SetAttributeValue(SAVE_ID_ATTR, save.Id);
                    saveEl.SetAttributeValue(SAVE_LLT_ATTR,
                        save.LastLaunchTime.ToString(CultureInfo.InvariantCulture));
                    el.Add(saveEl);
                }
                gameConfigsEl.Add(el);
            }

            var prefsEl = new XElement(USER_PREFS_EL,
                new XElement(USER_DISPLAY_NAME_EL, config.UserDisplayName),
                new XElement(USE_MANAGED_DECORATIONS_EL, config.UseManagedDecorations),
                new XElement(AUTO_CLOSE_SERVER_EL, config.AutoCloseServer));

            return new XDocument(new XElement(ROOT_EL, gameConfigsEl, prefsEl));
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: PASS (5 tests: missing→defaults, round-trip, atomic, no-dup, write-fail-untouched).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Services/HubConfigStore.cs src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs
git commit -m "feat(services): atomic HubConfigStore.Save (temp+Move, full reserialize, no dup-append)"
```

---

## Task 5: Recovering load — corrupt XML → backup + defaults

**Files:**
- Modify: `src/ReCap.Hub/Services/HubConfigStore.cs` (implement `BackUpCorruptFile`)
- Test: `src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs`

- [ ] **Step 1: Write the failing test** (append inside `HubConfigStoreTests`)

```csharp
        [Fact]
        public void Load_CorruptXml_BacksUpAndReturnsDefaults()
        {
            var fs = new FakeFileSystem();
            fs.Files[CfgPath] = "<hub><gameConfigs></hub>"; // malformed
            var store = NewStore(fs);

            var cfg = store.Load();

            AssertConfigsEqual(HubConfig.Default, cfg);
            Assert.True(fs.FileExists(CfgPath + ".corrupt.bak"));
            Assert.Equal("<hub><gameConfigs></hub>", fs.Files[CfgPath + ".corrupt.bak"]);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: FAIL — no `.corrupt.bak` created.

- [ ] **Step 3: Implement `BackUpCorruptFile`** (replace the stub)

```csharp
        void BackUpCorruptFile()
        {
            try
            {
                _fs.WriteAllText(_cfgPath + ".corrupt.bak", _fs.ReadAllText(_cfgPath));
            }
            catch
            {
                // Backup is best-effort; never let it block recovery to defaults.
            }
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubConfigStoreTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Services/HubConfigStore.cs src/ReCap.Hub.Tests/Services/HubConfigStoreTests.cs
git commit -m "feat(services): back up corrupt config.xml on parse failure, recover to defaults"
```

---

## Task 6: Register `IHubConfigStore` in the composition root

**Files:**
- Modify: `src/ReCap.Hub/Composition/HubServices.cs`
- Test: `src/ReCap.Hub.Tests/Composition/HubServicesTests.cs`

- [ ] **Step 1: Write the failing test** (append inside `HubServicesTests`)

```csharp
        [Fact]
        public void Register_AddsHubConfigStore()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<ReCap.Hub.Services.IHubConfigStore>();
            Assert.IsType<ReCap.Hub.Services.HubConfigStore>(store);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubServicesTests`
Expected: FAIL — `IHubConfigStore` not registered.

- [ ] **Step 3: Register it** (edit `HubServices.Register`)

```csharp
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<ReCap.Hub.Services.IHubConfigStore>(sp =>
            {
                var fs = sp.GetRequiredService<IFileSystem>();
                var dir = ReCap.Hub.Data.HubGlobalPaths.CfgPath;
                var path = System.IO.Path.Combine(dir, "config.xml");
                return new ReCap.Hub.Services.HubConfigStore(fs, dir, path);
            });
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False --filter FullyQualifiedName~HubServicesTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub/Composition/HubServices.cs src/ReCap.Hub.Tests/Composition/HubServicesTests.cs
git commit -m "feat(di): register IHubConfigStore in the composition root"
```

---

## Task 7: VM mapping — `GameConfigViewModel` ↔ `GameInstall`

Replace the XML-coupled `ref XElement` ctor / `WriteToXml` with typed mapping to the new POCOs. The save-file lookup behavior (read `<id>.xml` from `SavesPath`) is preserved exactly.

**Files:**
- Modify: `src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs`
- Modify: `src/ReCap.Hub/ViewModels/Local/SaveGameViewModel.cs`

- [ ] **Step 1: Add `ToSaveRef()` to `SaveGameViewModel`; delete its `WriteToXml`**

Remove:

```csharp
        public void WriteToXml(ref XElement el)
        {
            //Save();
            el.SetAttributeValue("id", Title);
            el.SetAttributeValue("lastLaunchTime", LastLaunchTime);
        }
```

Add (same place):

```csharp
        public ReCap.Hub.Domain.SaveRef ToSaveRef()
            => new ReCap.Hub.Domain.SaveRef { Id = Title, LastLaunchTime = LastLaunchTime };
```

- [ ] **Step 2: In `GameConfigViewModel`, replace the `ref XElement` ctor with a `GameInstall` ctor and add `ToGameInstall()`**

Remove the entire `public GameConfigViewModel(string gameInstallPath, string wineExecutable, string winePrefix, ref XElement element)` ctor (lines reading the `<save>` children from an `XElement`) **and** the `WriteToXml(ref XElement gameConfigEl)` method.

Add a new ctor (keeps the same save-file lookup logic, now driven by `SaveRef`s):

```csharp
        public GameConfigViewModel(ReCap.Hub.Domain.GameInstall install)
            : this(install.GameInstallPath, install.WineExecutable, install.WinePrefix)
        {
            Title = install.DisplayName ?? string.Empty;

            if (Directory.Exists(SavesPath))
            {
                foreach (var saveRef in install.Saves)
                {
                    if (string.IsNullOrEmpty(saveRef.Id))
                        continue;
                    string saveXmlPath = Path.Combine(SavesPath, saveRef.Id + ".xml");
                    if (!File.Exists(saveXmlPath))
                        continue;
                    Saves.Add(new SaveGameViewModel(saveXmlPath));
                }
            }

            if (TimeHelper.TryGetNewest(Saves, s => s.LastLaunchTime, out SaveGameViewModel lastPlayed))
                SelectedSave = lastPlayed;
        }

        public ReCap.Hub.Domain.GameInstall ToGameInstall()
            => new ReCap.Hub.Domain.GameInstall
            {
                GameInstallPath = GameInstallPath,
                SavesPath = SavesPath,
                WinePrefix = WinePrefixPath,
                WineExecutable = WineExecPath,
                DisplayName = Title,
                Saves = Saves.OrderBy(x => x.LastLaunchTime).Select(s => s.ToSaveRef()).ToList(),
            };
```

If, after removing `WriteToXml`, the `using System.Xml.Linq;` / `using System.Xml;` imports become unused, leave them — the compiler only warns. (They are removed in the Step 5 robustness sweep, not here.)

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors. (`HubData.cs` still references the old ctor/`WriteToXml` — **expected to break here**; Task 8 fixes `HubData`. If you prefer a green build at every commit, do Task 7 and Task 8 as one commit — see Task 8 Step 5.)

> NOTE: Tasks 7 and 8 are a single green checkpoint. Do not commit Task 7 alone if you require a compiling tree per commit; the combined commit is in Task 8.

---

## Task 8: Reduce `HubData` to a store-backed facade; final green

`HubData` keeps its `Instance`, INPC properties, and `GameConfigs` collection (so the ~12 call sites and the `x:Static` axaml bindings keep working), but **all persistence goes through `IHubConfigStore`**: `Load` builds VMs from `HubConfig`; `Save` builds a `HubConfig` from the VMs + current prefs and calls `store.Save`.

**Files:**
- Modify: `src/ReCap.Hub/Data/HubData.cs`

- [ ] **Step 1: Rewrite `HubData` as the facade** (full replacement file)

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using ReCap.Hub.Composition;
using ReCap.Hub.Domain;
using ReCap.Hub.Services;
using ReCap.Hub.ViewModels;

namespace ReCap.Hub.Data
{
    /// <summary>
    /// TRANSITIONAL facade (Step 1). Persistence is delegated to the injected
    /// <see cref="IHubConfigStore"/>; this type only maps <see cref="HubConfig"/> to/from
    /// ViewModels and preserves the existing <c>HubData.Instance</c> surface. Slated for
    /// deletion in Step 2 once LocalPlay/Preferences VMs map config to VMs directly.
    /// </summary>
    public class HubData : ViewModelBase
    {
        public static readonly HubData Instance = new HubData(HubServices.Get<IHubConfigStore>());

        readonly IHubConfigStore _store;

        ObservableCollection<GameConfigViewModel> _gameConfigs = new ObservableCollection<GameConfigViewModel>();
        public ObservableCollection<GameConfigViewModel> GameConfigs
        {
            get => _gameConfigs;
            protected set => RASIC(ref _gameConfigs, value);
        }

        string _userDisplayName = HubConfig.DefaultUserDisplayName;
        public string UserDisplayName
        {
            get => _userDisplayName;
            set => RASIC(ref _userDisplayName, value);
        }

        bool _useManagedDecorations = HubConfig.Default.UseManagedDecorations;
        public bool UseManagedDecorations
        {
            get => _useManagedDecorations;
            set => RASIC(ref _useManagedDecorations, value);
        }

        bool _autoCloseServer = true;
        public bool AutoCloseServer
        {
            get => _autoCloseServer;
            set => RASIC(ref _autoCloseServer, value);
        }

        HubData(IHubConfigStore store)
        {
            _store = store;
            Load();
            GameConfigs.CollectionChanged += GameConfigs_CollectionChanged;
        }

        void Load()
        {
            var cfg = _store.Load();
            UserDisplayName = cfg.UserDisplayName;
            UseManagedDecorations = cfg.UseManagedDecorations;
            AutoCloseServer = cfg.AutoCloseServer;

            foreach (var install in cfg.GameInstalls)
                GameConfigs.Add(new GameConfigViewModel(install));
        }

        void GameConfigs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => Save();

        public void Save()
        {
            var cfg = new HubConfig
            {
                UserDisplayName = UserDisplayName,
                UseManagedDecorations = UseManagedDecorations,
                AutoCloseServer = AutoCloseServer,
                GameInstalls = GameConfigs
                    .OrderBy(x => x.LastLaunchTime)
                    .Select(x => x.ToGameInstall())
                    .ToList(),
            };
            _store.Save(cfg);
        }
    }
}
```

Notes:
- The old `CfgPath` property is dropped from `HubData`. Two callers reference it: `Patcher.cs:79` (`HubData.Instance.CfgPath`) and a commented line in `LocalServer.cs:33`. Update `Patcher.cs:79` to use `HubGlobalPaths.CfgPath` directly (it is the same value the old property returned). The `LocalServer.cs:33` reference is already commented out — leave it.
- The `immediatelyWrite`-on-first-run behavior is preserved implicitly: callers that mutate `GameConfigs` trigger `Save()` via `CollectionChanged`; a fresh install simply has no file until the first real change, and `Load()` returns defaults — matching desired behavior (no more writing an empty file on startup).

- [ ] **Step 2: Fix the `CfgPath` caller in `Patcher.cs`**

Change `src/ReCap.Hub/Data/Patcher.cs:79` from:

```csharp
            string autoLoginPackageSrcPath = Path.Combine(HubData.Instance.CfgPath, AUTO_LOGIN_PACKAGE_NAME);
```

to:

```csharp
            string autoLoginPackageSrcPath = Path.Combine(HubGlobalPaths.CfgPath, AUTO_LOGIN_PACKAGE_NAME);
```

(Ensure `using ReCap.Hub.Data;` is present in `Patcher.cs` — it is in the same namespace `ReCap.Hub.Data`, so `HubGlobalPaths` resolves without a new using.)

- [ ] **Step 3: Build both configurations**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline`
Expected: Build succeeded, 0 errors.

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False`
Expected: PASS — all tests (Step 0's 4 + HubConfig 2 + HubConfigStore 6 + HubServices 1 new = prior + 9 new).

- [ ] **Step 5: Smoke-run the app**

Run: `dotnet src/ReCap.Hub/bin/Debug_Offline/net10.0/ResurrectionCapsuleHub.dll`
Expected: window "Resurrection Capsule Hub" opens, first-run **Locate Darkspore** screen shows, app exits clean. (No config file is written until a game config is added — verify no crash on startup.)

- [ ] **Step 6: Commit (Tasks 7 + 8 together — single green checkpoint)**

```bash
git add src/ReCap.Hub/ViewModels/Local/GameConfigViewModel.cs src/ReCap.Hub/ViewModels/Local/SaveGameViewModel.cs src/ReCap.Hub/Data/HubData.cs src/ReCap.Hub/Data/Patcher.cs
git commit -m "refactor(data): make HubData a store-backed facade; map GameConfigViewModel<->GameInstall"
```

---

## Self-Review (against spec §5)

- **`HubConfig` POCO (IReadOnlyList<GameInstall>, UserDisplayName, UseManagedDecorations, AutoCloseServer)** → Task 1. ✅ (`SavesPath`/`DisplayName`/`Saves` added to `GameInstall` beyond the spec's short list because they are required to stay green; confirmed with maintainer.)
- **`IHubConfigStore.Load()/Save()`** → Tasks 3–5. ✅
- **Atomic save (temp file → Move, no incremental Add)** → Task 4 (`Save`, `Save_IsAtomic…`, `Save_WriteFails…`). ✅
- **Recovering load (backup corrupt + defaults)** → Task 5. ✅
- **Built on `IFileSystem` (testable without disk)** → all store tests use `FakeFileSystem`. ✅
- **Data no longer knows about ViewModels** → `HubConfig`/`GameInstall`/`SaveRef` are VM-free; the store never constructs VMs. The VM mapping lives in the transitional `HubData` facade (deleted Step 2). ✅ (residual noted)
- **Migrate ~12 `HubData.Instance.Save()` sites via facade** → facade keeps `Instance`/`Save()`/properties/`GameConfigs`; only `Patcher.cs` `CfgPath` caller changed. ✅
- **Tests: round-trip; atomic; corrupt→defaults+backup; no dup-append after N saves; missing→defaults** → Tasks 3–5 cover all five. ✅
- **Bugs retired: crash-corruption, dup-append, data→VM inversion, hardcoded default username** → recovery (Task 5), full reserialize (Task 4), VM-free POCOs (Task 1), `"Player"` default + actually-written `userDisplayName` (Tasks 1, 4). ✅

**Residual / explicitly deferred:** nested per-save *data* (AccountModel files) untouched (Step 2/3); `HubData` facade and the full config→VM mapping move into LocalPlay VM are Step 2; unused `System.Xml` usings in `GameConfigViewModel` cleaned in Step 5 sweep.
