using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeManager.WebApi.Transactions
{
    /// <summary>
    /// Limit of the CSV export, per user (the <c>NameIdentifier</c> claim; per IP only without a valid token, which this
    /// <c>[Authorize]</c> route never reaches). A rejection is the same 429 as the auth limits'.
    /// </summary>
    public static class TransactionRateLimits
    {
        /// <summary>The CSV export: up to 36 months read and streamed per call.</summary>
        public const string Export = "TransactionsExport";

        private const int ExportPerWindow = 10;
        private static readonly TimeSpan ExportWindow = TimeSpan.FromMinutes(10);

        public static void AddPolicies(RateLimiterOptions options)
            => options.AddPolicy(Export, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
                    ? $"user:{userId}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = ExportPerWindow, Window = ExportWindow, QueueLimit = 0 }));
    }
}
