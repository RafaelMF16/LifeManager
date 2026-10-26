namespace LifeManager.Application.Auth.Emails
{
    /// <summary>
    /// The frontend pages the auth e-mails link to, built from the <c>frontendBaseUrl</c> configuration key (registered
    /// by the WebApi at startup).
    /// </summary>
    public sealed class FrontendLinks(string frontendBaseUrl)
    {
        private readonly string _baseUrl = frontendBaseUrl.TrimEnd('/');

        public string SetPasswordUrl(string token)
            => $"{_baseUrl}/auth/set-password?token={Uri.EscapeDataString(token)}";

        public string ResetPasswordUrl(string token)
            => $"{_baseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}";

        public string ForgotPasswordUrl()
            => $"{_baseUrl}/auth/forgot-password";
    }
}
