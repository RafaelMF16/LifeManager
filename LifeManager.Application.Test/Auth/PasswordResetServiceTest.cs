using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.UsersPreferences.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public partial class PasswordResetServiceTest : BaseTest
    {
        private const string OldPassword = "old-password";
        private const string NewPassword = "new-password";

        private readonly PasswordResetService _passwordResetService;
        private readonly FakeTimeProvider _clock;

        public PasswordResetServiceTest()
        {
            PasswordResetTokenSingleton.Instance.Clear();
            RefreshTokenSingleton.Instance.Clear();
            EmailCooldownSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            EmailSenderMock.Reset();

            _passwordResetService = ServiceProvider.GetRequiredService<PasswordResetService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.UtcNow = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        }

        [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
        private static partial Regex TokenInLink();

        private static string TokenFrom(string text) => Uri.UnescapeDataString(TokenInLink().Match(text).Groups[1].Value);

        private async Task<User> AddUserAsync(string email = "ana@example.com", Language language = Language.PTBR)
        {
            var hashedPassword = ServiceProvider.GetRequiredService<AuthService>().EncryptPassword(OldPassword);
            var user = User.Create("Ana", email, hashedPassword).Value!;
            return await ((UserRepositoryMock)ServiceProvider.GetRequiredService<IUserRepository>()).AddWithStarterDataAsync(user, Theme.Light, language, CancellationToken.None);
        }

        private async Task<string> RequestTokenAsync(string email = "ana@example.com")
        {
            await _passwordResetService.RequestAsync(new ForgotPasswordDto(email), CancellationToken.None);
            return TokenFrom(EmailSenderMock.Sent[^1].TextBody);
        }

        private async Task<bool> CanLogInAsync(string password)
            => (await ServiceProvider.GetRequiredService<UserService>().AuthenticateUserAsync(new LoginDto("ana@example.com", password), CancellationToken.None)).IsSuccess;

        [Fact]
        public async Task RequestAsync_ShouldStoreATokenAndSendTheLink_WhenTheEmailHasAnAccount()
        {
            await AddUserAsync();

            var result = await _passwordResetService.RequestAsync(new ForgotPasswordDto("ana@example.com"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stored = Assert.Single(PasswordResetTokenSingleton.Instance);
            var message = Assert.Single(EmailSenderMock.Sent);
            Assert.Equal("ana@example.com", message.ToAddress);
            Assert.Equal("Redefina sua senha", message.Subject);
            Assert.Contains($"{InjectionModule.TestFrontendBaseUrl}/auth/reset-password?token=", message.TextBody);
            Assert.Contains($"{InjectionModule.TestFrontendBaseUrl}/auth/reset-password?token=", message.HtmlBody);
            Assert.Equal(stored.TokenHash, EmailTokenHash.FromToken(TokenFrom(message.TextBody)).Value);
            Assert.Equal(_clock.UtcNow.AddHours(PasswordResetToken.ValidityHours), stored.ExpiresAt);
        }

        [Fact]
        public async Task RequestAsync_ShouldWriteTheEmailInTheUsersLanguage()
        {
            await AddUserAsync(language: Language.EN);

            await _passwordResetService.RequestAsync(new ForgotPasswordDto("ana@example.com"), CancellationToken.None);

            Assert.Equal("Reset your password", Assert.Single(EmailSenderMock.Sent).Subject);
        }

        [Fact]
        public async Task RequestAsync_ShouldSucceedAndSendNothing_WhenTheEmailHasNoAccount()
        {
            var result = await _passwordResetService.RequestAsync(new ForgotPasswordDto("nobody@example.com"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(EmailSenderMock.Sent);
            Assert.Empty(PasswordResetTokenSingleton.Instance);
            Assert.Empty(EmailCooldownSingleton.Instance);
        }

        [Fact]
        public async Task RequestAsync_ShouldReturnValidation_WhenTheEmailIsInvalid()
        {
            var result = await _passwordResetService.RequestAsync(new ForgotPasswordDto("not-an-email"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Empty(EmailSenderMock.Sent);
        }

        [Fact]
        public async Task RequestAsync_ShouldKeepTheFirstLink_WhenAskedAgainInsideTheCooldown()
        {
            await AddUserAsync();
            var firstToken = await RequestTokenAsync();
            _clock.UtcNow = _clock.UtcNow.AddSeconds(30);

            var result = await _passwordResetService.RequestAsync(new ForgotPasswordDto("ana@example.com"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(EmailSenderMock.Sent);
            Assert.True((await _passwordResetService.VerifyAsync(new EmailTokenDto(firstToken), CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task RequestAsync_ShouldInvalidateTheOldLink_WhenAskedAgainAfterTheCooldown()
        {
            await AddUserAsync();
            var firstToken = await RequestTokenAsync();
            _clock.UtcNow = _clock.UtcNow.Add(EmailCooldown.Duration);

            var secondToken = await RequestTokenAsync();

            Assert.Equal(2, EmailSenderMock.Sent.Count);
            Assert.Equal(AuthErrors.InvalidEmailToken, (await _passwordResetService.VerifyAsync(new EmailTokenDto(firstToken), CancellationToken.None)).Error);
            Assert.True((await _passwordResetService.VerifyAsync(new EmailTokenDto(secondToken), CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task VerifyAsync_ShouldReturnTheEmail_WhenTheLinkIsUsable()
        {
            await AddUserAsync();
            var token = await RequestTokenAsync();

            var result = await _passwordResetService.VerifyAsync(new EmailTokenDto(token), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("ana@example.com", result.Value!.Email);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknown-token")]
        public async Task VerifyAsync_ShouldReturnInvalidEmailToken_WhenTheLinkIsUnknown(string token)
        {
            var result = await _passwordResetService.VerifyAsync(new EmailTokenDto(token), CancellationToken.None);

            Assert.Equal(AuthErrors.InvalidEmailToken, result.Error);
        }

        [Fact]
        public async Task VerifyAsync_ShouldReturnInvalidEmailToken_WhenTheLinkExpired()
        {
            await AddUserAsync();
            var token = await RequestTokenAsync();
            _clock.UtcNow = _clock.UtcNow.AddHours(PasswordResetToken.ValidityHours);

            var result = await _passwordResetService.VerifyAsync(new EmailTokenDto(token), CancellationToken.None);

            Assert.Equal(AuthErrors.InvalidEmailToken, result.Error);
        }

        [Fact]
        public async Task ResetAsync_ShouldChangeThePasswordAndRevokeEverySession()
        {
            var user = await AddUserAsync();
            RefreshTokenSingleton.Instance.Add(RefreshToken.Create(user.Id!.Value, "session-hash", _clock.UtcNow.AddDays(7), _clock.UtcNow.AddDays(30), false).Value!);
            var token = await RequestTokenAsync();

            var result = await _passwordResetService.ResetAsync(new ResetPasswordDto(token, NewPassword), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.All(RefreshTokenSingleton.Instance.Where(refresh => refresh.UserId == user.Id), refresh => Assert.True(refresh.IsRevoked));
            Assert.False(await CanLogInAsync(OldPassword));
            Assert.True(await CanLogInAsync(NewPassword));
        }

        [Fact]
        public async Task ResetAsync_ShouldReturnInvalidEmailToken_WhenTheLinkWasAlreadyUsed()
        {
            await AddUserAsync();
            var token = await RequestTokenAsync();
            await _passwordResetService.ResetAsync(new ResetPasswordDto(token, NewPassword), CancellationToken.None);

            var result = await _passwordResetService.ResetAsync(new ResetPasswordDto(token, "another-password"), CancellationToken.None);

            Assert.Equal(AuthErrors.InvalidEmailToken, result.Error);
            Assert.True(await CanLogInAsync(NewPassword));
        }

        [Fact]
        public async Task ResetAsync_ShouldReportTheLinkBeforeThePassword_WhenBothAreInvalid()
        {
            var result = await _passwordResetService.ResetAsync(new ResetPasswordDto("unknown-token", "x"), CancellationToken.None);

            Assert.Equal(AuthErrors.InvalidEmailToken, result.Error);
        }

        [Fact]
        public async Task ResetAsync_ShouldKeepTheLinkUsable_WhenThePasswordIsInvalid()
        {
            await AddUserAsync();
            var token = await RequestTokenAsync();

            var result = await _passwordResetService.ResetAsync(new ResetPasswordDto(token, "x"), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.NotEqual(AuthErrors.InvalidEmailToken, result.Error);
            Assert.True(await CanLogInAsync(OldPassword));
            Assert.True((await _passwordResetService.VerifyAsync(new EmailTokenDto(token), CancellationToken.None)).IsSuccess);
        }
    }
}
