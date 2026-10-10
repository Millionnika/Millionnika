using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Millionnika.Api.Auth;

public sealed class AuthProtectionOptions
{
    public int LoginIpLimit { get; set; } = 30;
    public int LoginNameLimit { get; set; } = 10;
    public int RegistrationIpLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
    public int MaxBuckets { get; set; } = 10000;
}

// Process-local fixed windows. A full table refuses new keys instead of evicting active limits.
public sealed class AuthRequestLimiter(IOptions<AuthProtectionOptions> options, TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<string, (int Count, DateTimeOffset Until)> buckets = [];
    private DateTimeOffset nextCleanup;

    public int CheckIp(HttpContext context, bool registration) => Acquire(
        (registration ? "register:" : "login-ip:") + context.Connection.RemoteIpAddress?.MapToIPv6().ToString(),
        registration ? options.Value.RegistrationIpLimit : options.Value.LoginIpLimit);

    public int CheckLogin(string login) => Acquire("login-name:" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(login.Trim().ToUpperInvariant()))), options.Value.LoginNameLimit);

    private int Acquire(string key, int limit)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (now >= nextCleanup)
            {
                foreach (var expired in buckets.Where(pair => pair.Value.Until <= now).Select(pair => pair.Key).ToArray())
                    buckets.Remove(expired);
                nextCleanup = now.AddSeconds(Math.Min(30, options.Value.WindowSeconds));
            }
            if (!buckets.TryGetValue(key, out var bucket) || bucket.Until <= now)
            {
                if (!buckets.ContainsKey(key) && buckets.Count >= options.Value.MaxBuckets)
                    return Math.Max(1, (int)Math.Ceiling((buckets.Min(pair => pair.Value.Until) - now).TotalSeconds));
                bucket = (0, now.AddSeconds(options.Value.WindowSeconds));
            }
            if (bucket.Count >= limit)
                return Math.Max(1, (int)Math.Ceiling((bucket.Until - now).TotalSeconds));
            buckets[key] = (bucket.Count + 1, bucket.Until);
            return 0;
        }
    }

    public static IResult Rejected(HttpContext context, int seconds)
    {
        context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(new { message = $"Слишком много попыток. Повторите через {seconds} сек.", retryAfterSeconds = seconds },
            statusCode: StatusCodes.Status429TooManyRequests);
    }
}
