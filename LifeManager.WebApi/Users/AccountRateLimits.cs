using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeManager.WebApi.Users
{
    /// <summary>
    /// Limits of the "My account" actions, per user (the <c>NameIdentifier</c> claim; per IP only without a valid token,
    /// which these <c>[Authorize]</c> routes never reach). ASP.NET applies one policy per endpoint, so the export has only
    /// its own, stricter one.
    /// </summary>
    public static class AccountRateLimits
    {
        /// <summary>Password change and deletion request: each checks the password, so this also caps guessing it.</summary>
        public const string Sensitive = "AccountSensitive";

        /// <summary>The data export: a full read of the account, so few per hour.</summary>
        public const string Export = "AccountExport";

        private const int SensitivePerWindow = 10;
        private static readonly TimeSpan SensitiveWindow = TimeSpan.FromMinutes(10);
        private const int ExportPerWindow = 3;
        private static readonly TimeSpan ExportWindow = TimeSpan.FromHours(1);

        public static void AddPolicies(RateLimiterOptions options)
        {
            options.AddPolicy(Sensitive, httpContext => FixedWindowByUser(httpContext, SensitivePerWindow, SensitiveWindow));
            options.AddPolicy(Export, httpContext => FixedWindowByUser(httpContext, ExportPerWindow, ExportWindow));
        }

        private static RateLimitPartition<string> FixedWindowByUser(HttpContext httpContext, int permitLimit, TimeSpan window)
            => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                    ? $"user:{userId}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 });
    }
}
