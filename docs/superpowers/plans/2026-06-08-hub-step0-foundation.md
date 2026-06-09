# Hub Step 0 — Foundation (DI + test harness) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a dependency-injection composition root, a real xUnit test project, and the first injectable seam (`IFileSystem`) — with no change to runtime behavior.

**Architecture:** Introduce `Microsoft.Extensions.DependencyInjection`. A static `HubServices` builds a `ServiceProvider` at process start (`Program.Main`) and exposes it. The first registered service is `IFileSystem` (a thin wrapper over `System.IO`) — the seam every later step's persistence/config code will depend on. A new `ReCap.Hub.Tests` xUnit project proves the harness and tests `IFileSystem`. This is Step 0 of the spec `docs/superpowers/specs/2026-06-08-hub-modernization-design.md`; Steps 1–6 are separate plans.

**Tech Stack:** C# / net10.0, Avalonia, Microsoft.Extensions.DependencyInjection 10.0.0, xUnit 2.9.3.

---

## Notes for the implementer (read once)

- **Branch:** work on `develop` (already checked out).
- **Build-mode gotcha:** `ReCap.Hub.csproj` errors at build time unless `EnableReCapOnline` is set (a `BeforeTargets="Compile"` target). The plain `Debug` config does not set it. So **every build/test command in this plan passes `-p:EnableReCapOnline=False`.** (Simplifying this machinery is Step 6, a later plan — do not touch it here.)
- **No co-author:** commits must NOT contain any `Co-Authored-By` / "Generated with" trailer (maintainer's standing rule).
- All new public types are `public` so the test project (which references the Hub) can see them.

## File Structure

| File | Responsibility |
|------|----------------|
| `src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj` (create) | xUnit test project (net10), references the Hub |
| `src/ReCap.Hub.Tests/SanityTests.cs` (create) | Proves the test harness runs |
| `src/ReCap.Hub/Infrastructure/IFileSystem.cs` (create) | File-system seam interface |
| `src/ReCap.Hub/Infrastructure/FileSystem.cs` (create) | `System.IO`-backed implementation |
| `src/ReCap.Hub.Tests/Infrastructure/FileSystemTests.cs` (create) | Round-trips `FileSystem` against a temp dir |
| `src/ReCap.Hub/Composition/HubServices.cs` (create) | DI composition root + service registration |
| `src/ReCap.Hub.Tests/Composition/HubServicesTests.cs` (create) | Asserts the container resolves `IFileSystem` |
| `src/ReCap.Hub/ReCap.Hub.csproj` (modify) | Add the DI package reference |
| `src/ReCap.Hub/Program.cs` (modify) | Build the container in `Main` |
| `ReCap.Hub.sln` (modify) | Add the test project |

---

## Task 1: Create the xUnit test project

**Files:**
- Create: `src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj`
- Create: `src/ReCap.Hub.Tests/SanityTests.cs`
- Modify: `ReCap.Hub.sln`

- [ ] **Step 1: Create the test project file**

Create `src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj` (no Hub reference yet — added in Task 2):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the sanity test**

Create `src/ReCap.Hub.Tests/SanityTests.cs`:

```csharp
using Xunit;

namespace ReCap.Hub.Tests
{
    public class SanityTests
    {
        [Fact]
        public void Harness_Runs()
        {
            Assert.True(true);
        }
    }
}
```

- [ ] **Step 3: Add the project to the solution**

Run: `dotnet sln ReCap.Hub.sln add src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj`
Expected: `Project ... added to the solution.`

- [ ] **Step 4: Run the test to verify the harness works**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -v minimal`
Expected: PASS — `Passed!  - Failed: 0, Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj src/ReCap.Hub.Tests/SanityTests.cs ReCap.Hub.sln
git commit -m "test: add ReCap.Hub.Tests xUnit project"
```

---

## Task 2: Add the `IFileSystem` seam

**Files:**
- Create: `src/ReCap.Hub/Infrastructure/IFileSystem.cs`
- Create: `src/ReCap.Hub/Infrastructure/FileSystem.cs`
- Create: `src/ReCap.Hub.Tests/Infrastructure/FileSystemTests.cs`
- Modify: `src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj` (add Hub reference)

- [ ] **Step 1: Define the interface**

Create `src/ReCap.Hub/Infrastructure/IFileSystem.cs`. These six members are exactly what the
Step 1 config store needs (atomic write = write temp + Move-overwrite; recovery = read + Move backup):

```csharp
namespace ReCap.Hub.Infrastructure
{
    public interface IFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
        void Move(string sourcePath, string destPath, bool overwrite);
        void Delete(string path);
        void CreateDirectory(string path);
    }
}
```

- [ ] **Step 2: Reference the Hub from the test project**

In `src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj`, add this ItemGroup before `</Project>`:

```xml
  <ItemGroup>
    <ProjectReference Include="../ReCap.Hub/ReCap.Hub.csproj" />
  </ItemGroup>
```

- [ ] **Step 3: Write the failing test**

Create `src/ReCap.Hub.Tests/Infrastructure/FileSystemTests.cs`:

```csharp
using System;
using System.IO;
using ReCap.Hub.Infrastructure;
using Xunit;

namespace ReCap.Hub.Tests.Infrastructure
{
    public class FileSystemTests
    {
        [Fact]
        public void WriteThenRead_RoundTrips()
        {
            var fs = new FileSystem();
            var dir = Path.Combine(Path.GetTempPath(), "recaphub-tests-" + Guid.NewGuid().ToString("N"));
            fs.CreateDirectory(dir);
            var file = Path.Combine(dir, "x.txt");
            try
            {
                Assert.False(fs.FileExists(file));
                fs.WriteAllText(file, "hello");
                Assert.True(fs.FileExists(file));
                Assert.Equal("hello", fs.ReadAllText(file));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Move_Overwrite_ReplacesDestination()
        {
            var fs = new FileSystem();
            var dir = Path.Combine(Path.GetTempPath(), "recaphub-tests-" + Guid.NewGuid().ToString("N"));
            fs.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.txt");
            var dst = Path.Combine(dir, "dst.txt");
            try
            {
                fs.WriteAllText(dst, "old");
                fs.WriteAllText(src, "new");
                fs.Move(src, dst, overwrite: true);
                Assert.False(fs.FileExists(src));
                Assert.Equal("new", fs.ReadAllText(dst));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it fails to compile**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False -v minimal`
Expected: BUILD FAILS — `FileSystem` does not exist (`CS0246`).

- [ ] **Step 5: Implement `FileSystem`**

Create `src/ReCap.Hub/Infrastructure/FileSystem.cs`:

```csharp
using System.IO;

namespace ReCap.Hub.Infrastructure
{
    public sealed class FileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

        public void Move(string sourcePath, string destPath, bool overwrite)
            => File.Move(sourcePath, destPath, overwrite);

        public void Delete(string path) => File.Delete(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False -v minimal`
Expected: PASS — `Failed: 0, Passed: 3` (sanity + 2 filesystem).

- [ ] **Step 7: Commit**

```bash
git add src/ReCap.Hub/Infrastructure/IFileSystem.cs src/ReCap.Hub/Infrastructure/FileSystem.cs src/ReCap.Hub.Tests/Infrastructure/FileSystemTests.cs src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj
git commit -m "feat(infra): add IFileSystem seam + FileSystem impl"
```

---

## Task 3: Add the DI composition root

**Files:**
- Modify: `src/ReCap.Hub/ReCap.Hub.csproj` (add DI package)
- Create: `src/ReCap.Hub/Composition/HubServices.cs`
- Create: `src/ReCap.Hub.Tests/Composition/HubServicesTests.cs`

- [ ] **Step 1: Add the DI package reference**

In `src/ReCap.Hub/ReCap.Hub.csproj`, inside the existing `<ItemGroup>` that holds the other
`<PackageReference>` entries (the one containing `PeNet`), add:

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

- [ ] **Step 2: Write the failing test**

Create `src/ReCap.Hub.Tests/Composition/HubServicesTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ReCap.Hub.Composition;
using ReCap.Hub.Infrastructure;
using Xunit;

namespace ReCap.Hub.Tests.Composition
{
    public class HubServicesTests
    {
        [Fact]
        public void Register_AddsFileSystem()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            var fs = provider.GetRequiredService<IFileSystem>();
            Assert.IsType<FileSystem>(fs);
        }
    }
}
```

- [ ] **Step 3: Run the test to verify it fails to compile**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False -v minimal`
Expected: BUILD FAILS — `HubServices` does not exist (`CS0246`).

- [ ] **Step 4: Implement `HubServices`**

Create `src/ReCap.Hub/Composition/HubServices.cs`:

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Composition
{
    /// <summary>
    /// Process-wide dependency-injection composition root. Build() is called once at startup
    /// (Program.Main). Register() is kept separate so tests can compose an isolated container.
    /// </summary>
    public static class HubServices
    {
        public static IServiceProvider Provider { get; private set; }

        public static IServiceProvider Build()
        {
            var services = new ServiceCollection();
            Register(services);
            Provider = services.BuildServiceProvider();
            return Provider;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<IFileSystem, FileSystem>();
        }

        public static T Get<T>() where T : notnull
            => (Provider ?? throw new InvalidOperationException("HubServices.Build() was not called."))
                .GetRequiredService<T>();
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False -v minimal`
Expected: PASS — `Failed: 0, Passed: 4`.

- [ ] **Step 6: Commit**

```bash
git add src/ReCap.Hub/ReCap.Hub.csproj src/ReCap.Hub/Composition/HubServices.cs src/ReCap.Hub.Tests/Composition/HubServicesTests.cs
git commit -m "feat(di): add HubServices composition root, register IFileSystem"
```

---

## Task 4: Build the container at startup

**Files:**
- Modify: `src/ReCap.Hub/Program.cs`

- [ ] **Step 1: Add the using directive**

In `src/ReCap.Hub/Program.cs`, add to the using block (after `using ReCap.Hub.Data;` on line 9):

```csharp
using ReCap.Hub.Composition;
```

- [ ] **Step 2: Build the container before the GUI starts**

In `src/ReCap.Hub/Program.cs`, find this block (currently around lines 56–60):

```csharp
            if (CommandLine.Instance.ShowGUI)
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
```

Change it to:

```csharp
            HubServices.Build();

            if (CommandLine.Instance.ShowGUI)
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
```

- [ ] **Step 3: Build the app to verify it compiles**

Run: `dotnet build src/ReCap.Hub/ReCap.Hub.csproj -c Debug_Offline --nologo`
Expected: `0 Erro(s)` / `0 Error(s)` (MSB4011 warnings are pre-existing and expected).

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False -v minimal`
Expected: PASS — `Failed: 0, Passed: 4`.

- [ ] **Step 5: Smoke-run the app (no behavior change)**

Run: `dotnet src/ReCap.Hub/bin/Debug_Offline/net10.0/ResurrectionCapsuleHub.dll`
Expected: the window opens and shows the "Locate Darkspore" first-run screen exactly as before. Close it; clean exit.

- [ ] **Step 6: Commit**

```bash
git add src/ReCap.Hub/Program.cs
git commit -m "feat(di): build the service container at startup"
```

---

## Done criteria

- `dotnet test src/ReCap.Hub.Tests/ReCap.Hub.Tests.csproj -p:EnableReCapOnline=False` → 4 passing.
- App builds in `Debug_Offline` and runs unchanged.
- DI container + `IFileSystem` seam exist and are reachable via `HubServices.Get<T>()` — the foundation Step 1 (config store) will build on.

## Next plan

Step 1 — `HubData` → `HubConfig` + `IHubConfigStore` (atomic write + recovery, kills the
dup-append + corruption bugs). It will model the game-install + preferences data and route
`HubData`'s load/save through the store; the nested per-save XML (owned by
`GameConfigViewModel.WriteToXml`) is preserved as-is and tackled when `GameConfigViewModel` is
decomposed in the Step 2 plan.
