using System.Collections.Generic;
using ReCap.CommonUI.Util;

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
