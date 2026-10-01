namespace LifeManager.WebApi.Auth
{
    public static class RefreshTokenCookie
    {
        public const string Name = "refreshToken";
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
