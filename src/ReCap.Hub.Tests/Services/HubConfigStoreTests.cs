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
    }
}
