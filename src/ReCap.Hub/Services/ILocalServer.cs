using System;

namespace ReCap.Hub.Services
{
    public interface ILocalServer
    {
        /// <summary>Starts the local server and returns a handle; disposing it stops the server.</summary>
        IDisposable Start(string winePrefix, string wineExecutable);
    }
}
