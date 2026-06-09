using System;
using ReCap.Hub.Data;

namespace ReCap.Hub.Services
{
    public sealed class LocalServerAdapter : ILocalServer
    {
        public IDisposable Start(string winePrefix, string wineExecutable)
            => LocalServer.Instance.Start(winePrefix, wineExecutable);
    }
}
