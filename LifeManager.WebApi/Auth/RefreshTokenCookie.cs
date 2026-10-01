namespace LifeManager.WebApi.Auth
{
    public static class RefreshTokenCookie
    {
        public const string Name = "refreshToken";
        private const string PATH = "/api/Auth";
        private const short EXPIRATION_DAYS = 7;

        public static void Append(HttpResponse response, string refreshToken)
        {
            var options = CreateOptions();
            options.Expires = DateTimeOffset.UtcNow.AddDays(EXPIRATION_DAYS);

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
