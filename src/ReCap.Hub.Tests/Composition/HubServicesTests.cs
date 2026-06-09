using Microsoft.Extensions.DependencyInjection;
using ReCap.Hub.Composition;
using ReCap.Hub.Infrastructure;
using Xunit;

namespace ReCap.Hub.Tests.Composition
{
    public class HubServicesTests
    {
        [Fact]
        public void Register_AddsFileSystem()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            var fs = provider.GetRequiredService<IFileSystem>();
            Assert.IsType<FileSystem>(fs);
        }

        [Fact]
        public void Register_AddsHubConfigStore()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<ReCap.Hub.Services.IHubConfigStore>();
            Assert.IsType<ReCap.Hub.Services.HubConfigStore>(store);
        }

        [Fact]
        public void Register_AddsGameSessionAndCollaborators()
        {
            var services = new ServiceCollection();

            HubServices.Register(services);

            using var provider = services.BuildServiceProvider();
            Assert.IsType<ReCap.Hub.Services.GameSession>(provider.GetRequiredService<ReCap.Hub.Services.IGameSession>());
            Assert.IsType<ReCap.Hub.Services.PatcherAdapter>(provider.GetRequiredService<ReCap.Hub.Services.IPatcher>());
            Assert.IsType<ReCap.Hub.Services.LocalServerAdapter>(provider.GetRequiredService<ReCap.Hub.Services.ILocalServer>());
            Assert.IsType<ReCap.Hub.Services.GameLauncherAdapter>(provider.GetRequiredService<ReCap.Hub.Services.IGameLauncher>());
        }
    }
}
