using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.UsersPreferences.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public partial class RegistrationServiceTest : BaseTest
    {
        private readonly RegistrationService _registrationService;
        private readonly FakeTimeProvider _clock;

        public RegistrationServiceTest()
        {
            PendingRegistrationSingleton.Instance.Clear();
            EmailCooldownSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            EmailSenderMock.Reset();

            _registrationService = ServiceProvider.GetRequiredService<RegistrationService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.UtcNow = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        }

        [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
        private static partial Regex TokenInLink();

        private static string TokenFrom(string text) => Uri.UnescapeDataString(TokenInLink().Match(text).Groups[1].Value);

        private async Task<User> AddActiveUserAsync(string email, Language language)
        {
            var user = User.Create("Bia", email, "hash").Value!;
            return await ServiceProvider.GetRequiredService<IUserRepository>().AddWithStarterDataAsync(user, Theme.Light, language, CancellationToken.None);
        }

        [Fact]
        public async Task RegisterAsync_ShouldStoreThePendingSignUpAndSendTheConfirmation_WhenEmailIsNew()
        {
            var result = await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana", Language.EN, Theme.Dark), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var pending = Assert.Single(PendingRegistrationSingleton.Instance);
            Assert.Equal(("Ana", Language.EN, Theme.Dark), (pending.Name.Value, pending.Language, pending.Theme));
            var message = Assert.Single(EmailSenderMock.Sent);
            Assert.Equal("ana@example.com", message.ToAddress);
            Assert.Contains($"{InjectionModule.TestFrontendBaseUrl}/auth/set-password?token=", message.TextBody);
            Assert.Contains($"{InjectionModule.TestFrontendBaseUrl}/auth/set-password?token=", message.HtmlBody);
            Assert.Equal(pending.TokenHash, EmailTokenHash.FromToken(TokenFrom(message.TextBody)).Value);
        }

        [Fact]
        public async Task RegisterAsync_ShouldWriteTheEmailInTheSignUpLanguage()
        {
            await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana", Language.EN, null), CancellationToken.None);
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            await _registrationService.RegisterAsync(new RegisterDto("bia@example.com", "Bia", Language.PTBR, null), CancellationToken.None);

            Assert.Equal("Confirm your e-mail", EmailSenderMock.Sent[0].Subject);
            Assert.Equal("Confirme seu e-mail", EmailSenderMock.Sent[1].Subject);
        }

        [Fact]
        public async Task RegisterAsync_ShouldRenewTheSignUpAndInvalidateTheOldLink_WhenSignedUpAgainAfterTheCooldown()
        {
            await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana", null, null), CancellationToken.None);
            var firstToken = TokenFrom(EmailSenderMock.Sent[0].TextBody);
            _clock.UtcNow = _clock.UtcNow.Add(EmailCooldown.Duration);

            await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana Souza", Language.EN, null), CancellationToken.None);

            var pending = Assert.Single(PendingRegistrationSingleton.Instance);
            Assert.Equal("Ana Souza", pending.Name.Value);
            Assert.Equal(2, EmailSenderMock.Sent.Count);
            Assert.NotEqual(EmailTokenHash.FromToken(firstToken).Value, pending.TokenHash);
            Assert.Equal(pending.TokenHash, EmailTokenHash.FromToken(TokenFrom(EmailSenderMock.Sent[1].TextBody)).Value);
        }

        [Fact]
        public async Task RegisterAsync_ShouldChangeNothing_WhenSignedUpAgainInsideTheCooldown()
        {
            await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana", null, null), CancellationToken.None);
            var firstHash = PendingRegistrationSingleton.Instance[0].TokenHash;
            _clock.UtcNow = _clock.UtcNow.AddSeconds(30);

            var result = await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana Souza", null, null), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(EmailSenderMock.Sent);
            var pending = Assert.Single(PendingRegistrationSingleton.Instance);
            Assert.Equal(firstHash, pending.TokenHash);
            Assert.Equal("Ana", pending.Name.Value);
        }

        [Fact]
        public async Task RegisterAsync_ShouldOnlySendTheAccountExistsEmail_WhenTheEmailHasAnAccount()
        {
            await AddActiveUserAsync("bia@example.com", Language.EN);

            var result = await _registrationService.RegisterAsync(new RegisterDto("bia@example.com", "Someone", Language.PTBR, null), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(PendingRegistrationSingleton.Instance);
            Assert.Single(UserSingleton.Instance);
            var message = Assert.Single(EmailSenderMock.Sent);
            Assert.Equal("bia@example.com", message.ToAddress);
            Assert.Equal("You already have an account", message.Subject);
            Assert.Contains($"{InjectionModule.TestFrontendBaseUrl}/auth/forgot-password", message.TextBody);
            Assert.DoesNotContain("token=", message.TextBody);
        }

        [Fact]
        public async Task RegisterAsync_ShouldSendTheAccountExistsEmailOncePerCooldown()
        {
            await AddActiveUserAsync("bia@example.com", Language.PTBR);

            await _registrationService.RegisterAsync(new RegisterDto("bia@example.com", "Someone", null, null), CancellationToken.None);
            await _registrationService.RegisterAsync(new RegisterDto("bia@example.com", "Someone", null, null), CancellationToken.None);

            Assert.Equal("Você já tem uma conta", Assert.Single(EmailSenderMock.Sent).Subject);
        }

        [Fact]
        public async Task RegisterAsync_ShouldSucceedAndKeepTheSignUp_WhenTheEmailCannotBeSent()
        {
            EmailSenderMock.FailNextSends = true;

            var result = await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "Ana", null, null), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(PendingRegistrationSingleton.Instance);
        }

        [Theory]
        [InlineData("ana@example.com", "", "User.UserNameIsNullOrWhiteSpace")]
        [InlineData("ana", "Ana", "User.EmailIsInvalid")]
        [InlineData("", "Ana", "User.EmailIsNullOrWhiteSpace")]
        public async Task RegisterAsync_ShouldReturnTheValidationError_WhenNameOrEmailIsInvalid(string email, string name, string code)
        {
            var result = await _registrationService.RegisterAsync(new RegisterDto(email, name, null, null), CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(code, result.Error.Code);
            Assert.Empty(PendingRegistrationSingleton.Instance);
            Assert.Empty(EmailSenderMock.Sent);
        }

        [Fact]
        public async Task RegisterAsync_ShouldValidateTheName_WhenTheEmailHasAnAccount()
        {
            await AddActiveUserAsync("bia@example.com", Language.PTBR);

            var result = await _registrationService.RegisterAsync(new RegisterDto("bia@example.com", "", null, null), CancellationToken.None);

            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public async Task RegisterAsync_ShouldEscapeTheNameInTheHtml()
        {
            await _registrationService.RegisterAsync(new RegisterDto("ana@example.com", "<b>Ana</b>", null, null), CancellationToken.None);

            var message = Assert.Single(EmailSenderMock.Sent);
            Assert.DoesNotContain("<b>Ana</b>", message.HtmlBody);
            Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", message.HtmlBody);
        }
    }
}
