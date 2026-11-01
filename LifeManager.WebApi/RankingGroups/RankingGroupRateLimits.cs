using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace LifeManager.WebApi.RankingGroups
{
    /// <summary>
    /// Keeps a client from guessing invite codes. Per IP like the auth limits: the rate limiter runs before
    /// authentication, so the user isn't known yet. A rejection is the same 429 as the auth limits'.
    /// </summary>
    public static class RankingGroupRateLimits
    {
        /// <summary>The invite preview and join.</summary>
        public const string Invites = "RankingInvites";

        private const int InvitesPerWindow = 30;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        public static void AddPolicies(RateLimiterOptions options)
            => options.AddPolicy(Invites, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = InvitesPerWindow, Window = Window, QueueLimit = 0 }));
    }
}
