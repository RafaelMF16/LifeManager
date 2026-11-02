using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace LifeManager.WebApi.RankingGroups
{
    /// <summary>
    /// Keeps a client from guessing invite codes. Per user (the rate limiter runs after authentication), so it doesn't
    /// depend on the address the proxies pass on; the IP only for a request without a valid token, which the
    /// authorization refuses anyway. A rejection is the same 429 as the auth limits'.
    /// </summary>
    public static class RankingGroupRateLimits
    {
        /// <summary>The invite preview and join.</summary>
        public const string Invites = "RankingInvites";

        private const int InvitesPerWindow = 30;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        public static void AddPolicies(RateLimiterOptions options)
            => options.AddPolicy(Invites, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                    ? $"user:{userId}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = InvitesPerWindow, Window = Window, QueueLimit = 0 }));
    }
}
