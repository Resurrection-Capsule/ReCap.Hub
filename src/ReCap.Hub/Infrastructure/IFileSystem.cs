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
