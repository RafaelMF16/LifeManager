namespace LifeManager.WebApi.Auth
{
    public static class RefreshTokenCookie
    {
        // Firebase Hosting forwards only a cookie named "__session" to the Cloud Run service behind its "/api/**" rewrite.
        public const string Name = "__session";
        private const string PATH = "/api/Auth";

        /// <param name="expiresAt">The refresh token's own expiration, so the cookie never outlives the token (or the session cap).</param>
        public static void Append(HttpResponse response, string refreshToken, DateTimeOffset expiresAt)
        {
            var options = CreateOptions();
            options.Expires = expiresAt;

            response.Cookies.Append(Name, refreshToken, options);
        }

        public static void Delete(HttpResponse response)
            => response.Cookies.Delete(Name, CreateOptions());

        public static string? Read(HttpRequest request)
            => request.Cookies[Name];

        private static CookieOptions CreateOptions() => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = PATH
        };
    }
}
