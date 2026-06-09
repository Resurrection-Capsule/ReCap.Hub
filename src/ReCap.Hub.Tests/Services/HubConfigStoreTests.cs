using System.Linq;
using ReCap.Hub.Domain;
using ReCap.Hub.Services;
using Xunit;

namespace ReCap.Hub.Tests.Services
{
    public class HubConfigStoreTests
    {
        const string CfgDir = "/cfg";
        const string CfgPath = "/cfg/config.xml";

        static HubConfigStore NewStore(FakeFileSystem fs) => new HubConfigStore(fs, CfgDir, CfgPath);

        [Fact]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var fs = new FakeFileSystem();

            var cfg = NewStore(fs).Load();

            Assert.Equal(HubConfig.DefaultUserDisplayName, cfg.UserDisplayName);
            Assert.Empty(cfg.GameInstalls);
            Assert.True(cfg.AutoCloseServer);
        }

        static HubConfig SampleConfig() => new HubConfig
        {
            UserDisplayName = "Ada",
            UseManagedDecorations = false,
            AutoCloseServer = false,
            GameInstalls = new[]
            {
                new GameInstall
                {
                    GameInstallPath = "/games/darkspore",
                    SavesPath = "/saves",
                    WinePrefix = "/wine/pfx",
                    WineExecutable = "/usr/bin/wine",
                    DisplayName = "My Install",
                    Saves = new[]
                    {
                        new SaveRef { Id = "alice", LastLaunchTime = 123.5 },
                        new SaveRef { Id = "bob", LastLaunchTime = 200 },
                    },
                },
            },
        };

        static void AssertConfigsEqual(HubConfig expected, HubConfig actual)
        {
            Assert.Equal(expected.UserDisplayName, actual.UserDisplayName);
            Assert.Equal(expected.UseManagedDecorations, actual.UseManagedDecorations);
            Assert.Equal(expected.AutoCloseServer, actual.AutoCloseServer);
            Assert.Equal(expected.GameInstalls.Count, actual.GameInstalls.Count);
            for (int i = 0; i < expected.GameInstalls.Count; i++)
            {
                var e = expected.GameInstalls[i];
                var a = actual.GameInstalls[i];
                Assert.Equal(e.GameInstallPath, a.GameInstallPath);
                Assert.Equal(e.SavesPath, a.SavesPath);
                Assert.Equal(e.WinePrefix, a.WinePrefix);
                Assert.Equal(e.WineExecutable, a.WineExecutable);
                Assert.Equal(e.DisplayName, a.DisplayName);
                Assert.Equal(e.Saves.Count, a.Saves.Count);
                for (int j = 0; j < e.Saves.Count; j++)
                {
                    Assert.Equal(e.Saves[j].Id, a.Saves[j].Id);
                    Assert.Equal(e.Saves[j].LastLaunchTime, a.Saves[j].LastLaunchTime);
                }
            }
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var fs = new FakeFileSystem();
            var store = NewStore(fs);
            var cfg = SampleConfig();

            store.Save(cfg);
            var loaded = store.Load();

            AssertConfigsEqual(cfg, loaded);
        }

        [Fact]
        public void Save_IsAtomic_WritesViaTempThenMove_NoTempLeftBehind()
        {
            var fs = new FakeFileSystem();
            NewStore(fs).Save(SampleConfig());

            Assert.True(fs.FileExists(CfgPath));
            Assert.DoesNotContain(fs.Files.Keys, k => k != CfgPath && k.StartsWith(CfgPath));
        }

        [Fact]
        public void Save_NSaves_DoNotDuplicatePreferences()
        {
            var fs = new FakeFileSystem();
            var store = NewStore(fs);

            for (int i = 0; i < 5; i++)
                store.Save(SampleConfig());

            var doc = System.Xml.Linq.XDocument.Parse(fs.Files[CfgPath]);
            Assert.Single(doc.Root.Elements("preferences"));
            Assert.Single(doc.Root.Elements("preferences").First().Elements("autoCloseServer"));
            Assert.Single(doc.Root.Elements("gameConfigs"));
            Assert.Single(doc.Root.Elements("gameConfigs").First().Elements("gameConfig"));
        }

        [Fact]
        public void Save_WriteFailsToTemp_LeavesExistingFileUntouched()
        {
            var fs = new FakeFileSystem();
            // Seed an existing good file via a successful save.
            NewStore(fs).Save(SampleConfig());
            string good = fs.Files[CfgPath];

            // Now make any temp write fail and attempt a different save.
            fs.FailWriteWhen = path => path != CfgPath; // temp path differs from target
            var failing = NewStore(fs);

            Assert.ThrowsAny<System.Exception>(() =>
                failing.Save(new HubConfig { UserDisplayName = "should-not-persist" }));

            Assert.Equal(good, fs.Files[CfgPath]); // target unchanged
        }
    }
}
