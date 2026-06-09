using ReCap.Hub.Data;

namespace ReCap.Hub.Services
{
    public sealed class PatcherAdapter : IPatcher
    {
        public int PatchGame(bool exeMissing, string exeSrcPath, string exeDestPath,
                             bool autoLoginPackageMissing, string autoLoginPackageDestPath)
            => Patcher.PatchGame(exeMissing, exeSrcPath, exeDestPath,
                                 autoLoginPackageMissing, autoLoginPackageDestPath);
    }
}
