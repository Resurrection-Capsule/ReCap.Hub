using System.Linq;
using ReCap.Hub.Domain;
using Xunit;

namespace ReCap.Hub.Tests.Domain
{
    public class HubConfigTests
    {
        [Fact]
        public void Default_HasNeutralUsernameNoInstallsAndAutoCloseOn()
        {
            var cfg = HubConfig.Default;

            Assert.Equal("Player", cfg.UserDisplayName);
            Assert.Empty(cfg.GameInstalls);
            Assert.True(cfg.AutoCloseServer);
        }

        [Fact]
        public void Default_UserDisplayNameIsNotTheOldHardcodedName()
        {
            Assert.NotEqual("Splitwirez", HubConfig.Default.UserDisplayName);
        }
    }
}
