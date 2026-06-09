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
}
