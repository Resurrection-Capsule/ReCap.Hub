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
