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
    }
}
