using LifeManager.Domain.Auth.Errors;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace LifeManager.WebApi.Auth
{
    /// <summary>
    /// Per-IP limits on the anonymous auth endpoints that send e-mails or take e-mail tokens, on top of the per-e-mail
    /// cooldown: they keep one client from sending e-mails to many addresses or guessing tokens. A rejection is a 429
    /// with the <see cref="AuthErrors.TooManyRequests"/> body. The IP is <c>RemoteIpAddress</c>, already rewritten by the
    /// forwarded headers.
    /// </summary>
    public static class AuthRateLimits
    {
        /// <summary>Register and ForgotPassword: each call may send an e-mail.</summary>
        public const string EmailRequests = "EmailRequests";

        /// <summary>The Verify, Complete and ResetPassword calls, which take the token from an e-mail link.</summary>
        public const string EmailTokens = "EmailTokens";

        private const int EmailRequestsPerWindow = 10;
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
                options.OnRejected = WriteTooManyRequestsAsync;
            });
        }

        private static RateLimitPartition<string> FixedWindowByIp(HttpContext httpContext, int permitLimit)
            => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window, QueueLimit = 0 });

        private static string ClientIp(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;

            // The IP shows whether the limit is per client or, behind a proxy that isn't unwrapped, shared by everyone.
            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AuthRateLimits))
                .LogWarning("Rate limit hit on {Path} for {ClientIp}", httpContext.Request.Path, ClientIp(httpContext));

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(AuthErrors.TooManyRequests, SerializerOptions), cancellationToken);
        }
    }
}
