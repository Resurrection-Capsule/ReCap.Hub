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
