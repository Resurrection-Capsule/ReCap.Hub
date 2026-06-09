using System;
using System.IO;
using ReCap.Hub.Infrastructure;
using Xunit;

namespace ReCap.Hub.Tests.Infrastructure
{
    public class FileSystemTests
    {
        [Fact]
        public void WriteThenRead_RoundTrips()
        {
            var fs = new FileSystem();
            var dir = Path.Combine(Path.GetTempPath(), "recaphub-tests-" + Guid.NewGuid().ToString("N"));
            fs.CreateDirectory(dir);
            var file = Path.Combine(dir, "x.txt");
            try
            {
                Assert.False(fs.FileExists(file));
                fs.WriteAllText(file, "hello");
                Assert.True(fs.FileExists(file));
                Assert.Equal("hello", fs.ReadAllText(file));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Move_Overwrite_ReplacesDestination()
        {
            var fs = new FileSystem();
            var dir = Path.Combine(Path.GetTempPath(), "recaphub-tests-" + Guid.NewGuid().ToString("N"));
            fs.CreateDirectory(dir);
            var src = Path.Combine(dir, "src.txt");
            var dst = Path.Combine(dir, "dst.txt");
            try
            {
                fs.WriteAllText(dst, "old");
                fs.WriteAllText(src, "new");
                fs.Move(src, dst, overwrite: true);
                Assert.False(fs.FileExists(src));
                Assert.Equal("new", fs.ReadAllText(dst));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
