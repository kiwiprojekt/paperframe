using System.Collections.Concurrent;

namespace paperframe_server;

public class AuthService : IDisposable
{
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();
    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(7);
    private readonly TimeProvider _timeProvider;
    private readonly Timer _cleanupTimer;

    public AuthService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _cleanupTimer = new Timer(_ => PurgeExpired(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    private void PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var key in _sessions.Keys)
            if (_sessions.TryGetValue(key, out var expiry) && now >= expiry)
                _sessions.TryRemove(key, out _);
    }

    public void Dispose() => _cleanupTimer.Dispose();

    public string CreateSession()
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = _timeProvider.GetUtcNow().UtcDateTime + SessionDuration;
        return token;
    }

    public bool ValidateSession(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (_sessions.TryGetValue(token, out var expiry))
        {
            if (_timeProvider.GetUtcNow().UtcDateTime < expiry)
            {
                _sessions[token] = _timeProvider.GetUtcNow().UtcDateTime + SessionDuration;
                return true;
            }
            _sessions.TryRemove(token, out _);
        }
        return false;
    }

    public void RevokeSession(string? token)
    {
        if (!string.IsNullOrEmpty(token))
            _sessions.TryRemove(token, out _);
    }

    public void RevokeAllSessions()
    {
        _sessions.Clear();
    }
}
