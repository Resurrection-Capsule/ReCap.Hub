using System.IO;

namespace ReCap.Hub.Infrastructure
{
    public sealed class FileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

        public void Move(string sourcePath, string destPath, bool overwrite)
            => File.Move(sourcePath, destPath, overwrite);

        public void Delete(string path) => File.Delete(path);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    }
}
