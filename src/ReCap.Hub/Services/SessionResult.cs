using System;

namespace ReCap.Hub.Services
{
    public sealed class SessionResult
    {
        public bool Success { get; init; }
        public Exception Error { get; init; }
        public static SessionResult Ok() => new SessionResult { Success = true };
        public static SessionResult Fail(Exception e) => new SessionResult { Success = false, Error = e };
    }
}
