using LifeManager.Domain.Auth.Errors;
using LifeManager.WebApi.RankingGroups;
using LifeManager.WebApi.Transactions;
using LifeManager.WebApi.Users;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace LifeManager.WebApi.Auth
{
    /// <summary>
    /// Limits on the anonymous auth endpoints. Per IP on the ones that send e-mails or take e-mail tokens (on top of the
    /// per-e-mail cooldown: they keep one client from sending e-mails to many addresses or guessing tokens) and on the
    /// login (on top of the per e-mail and IP block, see <c>LoginAttempt</c>); per session on the refresh. A rejection is
    /// a 429 with the <see cref="AuthErrors.TooManyRequests"/> body. The IP is <c>RemoteIpAddress</c>, already rewritten
    /// by the forwarded headers.
    /// </summary>
    public static class AuthRateLimits
    {
        /// <summary>Register and ForgotPassword: each call may send an e-mail.</summary>
        public const string EmailRequests = "EmailRequests";

        /// <summary>The Verify, Complete and ResetPassword calls, which take the token from an e-mail link.</summary>
        public const string EmailTokens = "EmailTokens";

        /// <summary>Login: each call checks a password.</summary>
        public const string Login = "Login";

        /// <summary>Refresh: each call trades the session cookie for new tokens.</summary>
        public const string Refresh = "Refresh";

        private const int EmailRequestsPerWindow = 10;
        // Roomy on purpose: if the address seen is the Firebase Hosting's, every user shares this counter, so it only
        // stops a flood. Guessing a password is stopped by the per e-mail and IP block.
        private const int LoginPerWindow = 100;
        // Per session (every reload and new tab refreshes, since the frontend keeps the access token in memory only).
        private const int RefreshPerWindow = 60;
        private const int EmailTokensPerWindow = 20;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        public static void Configure(IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.AddPolicy(EmailRequests, httpContext => FixedWindowByIp(httpContext, EmailRequestsPerWindow));
                options.AddPolicy(EmailTokens, httpContext => FixedWindowByIp(httpContext, EmailTokensPerWindow));
                options.AddPolicy(Login, httpContext => FixedWindowByIp(httpContext, LoginPerWindow));
                options.AddPolicy(Refresh, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    SessionPartition(httpContext),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = RefreshPerWindow, Window = Window, QueueLimit = 0 }));
                RankingGroupRateLimits.AddPolicies(options);
                AccountRateLimits.AddPolicies(options);
                TransactionRateLimits.AddPolicies(options);
                options.OnRejected = WriteTooManyRequestsAsync;
            });
        }

        private static RateLimitPartition<string> FixedWindowByIp(HttpContext httpContext, int permitLimit)
            => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window, QueueLimit = 0 });

        /// <summary>
        /// The session cookie's SHA-256, so the counter doesn't depend on the address the proxies pass on and the token
        /// itself is never kept; the IP without a cookie. Made-up cookies each get a counter of their own, which is fine:
        /// a refresh token can't be guessed, and each try costs one indexed lookup.
        /// </summary>
        private static string SessionPartition(HttpContext httpContext)
            => RefreshTokenCookie.Read(httpContext.Request) is { Length: > 0 } refreshToken
                ? $"session:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)))}"
                : $"ip:{ClientIp(httpContext)}";

        /// <summary>The client's address as the forwarded headers left it; also the IP half of the login's blocked pairs.</summary>
        public static string ClientIp(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;

            // The IP shows whether the limit is per client or, behind a proxy that isn't unwrapped, shared by everyone. The
            // chain shows which: X-Forwarded-For keeps the entries the forwarded headers left, X-Original-For the address
            // the connection came from. If the client's own IP is among the entries left, the IP used is a proxy's.
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AuthRateLimits))
                .LogWarning(
                    "Rate limit hit on {Path} for {ClientIp} (X-Forwarded-For left: {ForwardedFor}; X-Original-For: {OriginalFor})",
                    httpContext.Request.Path,
                    ClientIp(httpContext),
                    httpContext.Request.Headers[ForwardedHeadersDefaults.XForwardedForHeaderName].ToString(),
                    httpContext.Request.Headers[ForwardedHeadersDefaults.XOriginalForHeaderName].ToString());

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(AuthErrors.TooManyRequests, SerializerOptions), cancellationToken);
        }
    }
}
