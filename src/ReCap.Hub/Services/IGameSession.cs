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
