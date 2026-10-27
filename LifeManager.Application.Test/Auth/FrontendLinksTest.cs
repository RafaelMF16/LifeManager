using LifeManager.Application.Auth.Emails;

namespace LifeManager.Application.Test.Auth
{
    public class FrontendLinksTest
    {
        [Fact]
        public void SetPasswordUrl_ShouldPointToTheSetPasswordPage_WhenTokenIsGiven()
        {
            var links = new FrontendLinks("https://localhost:5173");

            Assert.Equal("https://localhost:5173/auth/set-password?token=abc_-123", links.SetPasswordUrl("abc_-123"));
        }

        [Fact]
        public void ResetPasswordUrl_ShouldPointToTheResetPasswordPage_WhenTokenIsGiven()
        {
            var links = new FrontendLinks("https://localhost:5173");

            Assert.Equal("https://localhost:5173/auth/reset-password?token=abc_-123", links.ResetPasswordUrl("abc_-123"));
        }

        [Fact]
        public void ForgotPasswordUrl_ShouldPointToTheForgotPasswordPage()
        {
            var links = new FrontendLinks("https://localhost:5173");

            Assert.Equal("https://localhost:5173/auth/forgot-password", links.ForgotPasswordUrl());
        }

        [Fact]
        public void SetPasswordUrl_ShouldNotDoubleTheSlash_WhenBaseUrlEndsWithSlash()
        {
            var links = new FrontendLinks("https://app.example.com/");

            Assert.Equal("https://app.example.com/auth/set-password?token=t", links.SetPasswordUrl("t"));
        }

        [Fact]
        public void SetPasswordUrl_ShouldEscapeTheToken_WhenItHasReservedCharacters()
        {
            var links = new FrontendLinks("https://localhost:5173");

            Assert.Equal("https://localhost:5173/auth/set-password?token=a%2Bb%3D", links.SetPasswordUrl("a+b="));
        }
    }
}
