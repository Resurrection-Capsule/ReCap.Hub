using System;
using System.Collections.Generic;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Tests.Services
{
    /// <summary>In-memory IFileSystem. Paths are compared ordinally (case-sensitive is fine for tests).</summary>
    public sealed class FakeFileSystem : IFileSystem
    {
        public readonly Dictionary<string, string> Files = new();
        public readonly HashSet<string> Dirs = new();

        /// <summary>If set, WriteAllText throws for any path the predicate returns true for.</summary>
        public Func<string, bool> FailWriteWhen;

        public bool FileExists(string path) => Files.ContainsKey(path);

        public string ReadAllText(string path) => Files[path];

        public void WriteAllText(string path, string contents)
        {
            if (FailWriteWhen != null && FailWriteWhen(path))
                throw new System.IO.IOException("simulated write failure: " + path);
            Files[path] = contents;
        }

        public void Move(string sourcePath, string destPath, bool overwrite)
        {
            if (!Files.ContainsKey(sourcePath))
                throw new System.IO.FileNotFoundException(sourcePath);
            if (Files.ContainsKey(destPath) && !overwrite)
                throw new System.IO.IOException("dest exists: " + destPath);
            Files[destPath] = Files[sourcePath];
            Files.Remove(sourcePath);
        }

        public void Delete(string path) => Files.Remove(path);

        public void CreateDirectory(string path) => Dirs.Add(path);
    }
}
