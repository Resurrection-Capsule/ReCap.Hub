using System.Threading;
using System.Threading.Tasks;

namespace ReCap.Hub.Services
{
    public interface IGameSession
    {
        Task<SessionResult> PlayAsync(GameSessionRequest request, CancellationToken cancellationToken);
    }
}
