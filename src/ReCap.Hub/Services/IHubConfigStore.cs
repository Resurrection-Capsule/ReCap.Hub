using ReCap.Hub.Domain;

namespace ReCap.Hub.Services
{
    public interface IHubConfigStore
    {
        HubConfig Load();
        void Save(HubConfig config);
    }
}
