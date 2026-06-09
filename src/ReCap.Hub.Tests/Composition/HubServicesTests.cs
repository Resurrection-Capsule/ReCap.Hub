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
    }
}
