using Microsoft.Extensions.Caching.Memory;

namespace HM.AdminPanel.Services;

public class LoginThrottleService
{
    private const int MaxAttempts   = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    public LoginThrottleService(IMemoryCache cache) => _cache = cache;

    public bool IsBlocked(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        return _cache.TryGetValue(Key(ip), out int n) && n >= MaxAttempts;
    }

    public void RecordFailure(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return;
        var n = _cache.TryGetValue(Key(ip), out int v) ? v + 1 : 1;
        _cache.Set(Key(ip), n, Window);
    }

    public void Reset(string ip)
    {
        if (!string.IsNullOrEmpty(ip)) _cache.Remove(Key(ip));
    }

    private static string Key(string ip) => $"admin-login-throttle::{ip}";
}
