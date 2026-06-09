namespace ReCap.Hub.Services
{
    public interface IPatcher
    {
        int PatchGame(bool exeMissing, string exeSrcPath, string exeDestPath,
                      bool autoLoginPackageMissing, string autoLoginPackageDestPath);
    }
}
