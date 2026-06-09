using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReCap.Hub.Services;
using Xunit;

namespace ReCap.Hub.Tests.Services
{
    public class GameSessionTests
    {
        static GameSessionRequest Req(bool autoClose, bool needsPatch = true) => new GameSessionRequest
        {
            GameExePath = "/game/DarksporeBin/Darkspore_ReCapPatched.exe",
            GameOriginalExePath = "/game/DarksporeBin/Darkspore.exe",
            GameBinDir = "/game/DarksporeBin",
            AutoLoginPackageDestPath = "/game/Data/0ReCapAutoLogin.package",
            WinePrefix = null,
            WineExecutable = null,
            ExeMissing = needsPatch,
            AutoLoginPackageMissing = needsPatch,
            AutoCloseServer = autoClose,
        };

        static (GameSession s, CallLog log, FakePatcher p, FakeLocalServer srv, FakeGameLauncher l) New()
        {
            var log = new CallLog();
            var p = new FakePatcher(log);
            var srv = new FakeLocalServer(log);
            var l = new FakeGameLauncher(log);
            return (new GameSession(p, srv, l), log, p, srv, l);
        }

        [Fact]
        public async Task PlayAsync_AutoClose_OrdersPatchStartLaunchStop_AndSucceeds()
        {
            var (s, log, _, srv, _) = New();

            var result = await s.PlayAsync(Req(autoClose: true), CancellationToken.None);

            Assert.Equal(new[] { "patch", "server.start", "launch", "server.stop" }, log.Events.ToArray());
            Assert.True(result.Success);
            Assert.True(srv.Handles.Single().Disposed);
        }

        [Fact]
        public async Task PlayAsync_NoAutoClose_LeavesServerRunning()
        {
            var (s, log, _, srv, _) = New();

            var result = await s.PlayAsync(Req(autoClose: false), CancellationToken.None);

            Assert.Equal(new[] { "patch", "server.start", "launch" }, log.Events.ToArray());
            Assert.True(result.Success);
            Assert.False(srv.Handles.Single().Disposed);
        }

        [Fact]
        public async Task PlayAsync_SkipsPatch_WhenNothingMissing()
        {
            var (s, log, p, _, _) = New();

            await s.PlayAsync(Req(autoClose: true, needsPatch: false), CancellationToken.None);

            Assert.Equal(0, p.Calls);
            Assert.Equal(new[] { "server.start", "launch", "server.stop" }, log.Events.ToArray());
        }

        [Fact]
        public async Task PlayAsync_LaunchFails_ReturnsFailure_ButStillStopsServerWhenAutoClose()
        {
            var (s, log, _, srv, l) = New();
            l.Result = new System.InvalidOperationException("boom");

            var result = await s.PlayAsync(Req(autoClose: true), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Same(l.Result, result.Error);
            Assert.True(srv.Handles.Single().Disposed);
            Assert.Equal(new[] { "patch", "server.start", "launch", "server.stop" }, log.Events.ToArray());
        }
    }
}
