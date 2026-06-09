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
