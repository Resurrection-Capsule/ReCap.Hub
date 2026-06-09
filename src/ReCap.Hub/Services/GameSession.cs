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
