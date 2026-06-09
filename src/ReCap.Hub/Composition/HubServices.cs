#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using ReCap.Hub.Infrastructure;

namespace ReCap.Hub.Composition
{
    /// <summary>
    /// Process-wide dependency-injection composition root. Build() is called once at startup
    /// (Program.Main). Register() is kept separate so tests can compose an isolated container.
    /// </summary>
    public static class HubServices
    {
        public static IServiceProvider? Provider { get; private set; }

        public static IServiceProvider Build()
        {
            var services = new ServiceCollection();
            Register(services);
            Provider = services.BuildServiceProvider();
            return Provider;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<ReCap.Hub.Services.IHubConfigStore>(sp =>
            {
                var fs = sp.GetRequiredService<IFileSystem>();
                var dir = ReCap.Hub.Data.HubGlobalPaths.CfgPath;
                var path = System.IO.Path.Combine(dir, "config.xml");
                return new ReCap.Hub.Services.HubConfigStore(fs, dir, path);
            });
        }

        public static T Get<T>() where T : notnull
            => (Provider ?? throw new InvalidOperationException("HubServices.Build() was not called."))
                .GetRequiredService<T>();
    }
}
