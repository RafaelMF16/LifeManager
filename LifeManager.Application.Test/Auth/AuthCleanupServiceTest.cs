using LifeManager.Application.Auth.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Auth
{
    [Collection("ApplicationServices")]
    public class AuthCleanupServiceTest : BaseTest
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private readonly AuthCleanupService _authCleanupService;

        public AuthCleanupServiceTest()
        {
            PendingRegistrationSingleton.Instance.Clear();
            PasswordResetTokenSingleton.Instance.Clear();
            EmailCooldownSingleton.Instance.Clear();

            _authCleanupService = ServiceProvider.GetRequiredService<AuthCleanupService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).UtcNow = Now;
        }

        private static EmailTokenHash HashOf(string token) => EmailTokenHash.FromToken(token).Value!;

        // Creates a sign-up whose link expired `daysAgo` days before Now.
        private static void SeedPendingExpired(string email, int daysAgo)
            => PendingRegistrationSingleton.Instance.Add(PendingRegistration.Create(
                "Ana", email, Language.PTBR, Theme.Light, HashOf(email),
                Now.AddDays(-daysAgo).AddHours(-PendingRegistration.ValidityHours)).Value!);

        // Creates a reset link that expired `daysAgo` days before Now.
        private static PasswordResetToken SeedResetExpired(string token, int daysAgo)
        {
            var resetToken = PasswordResetToken.Create(new UserId(1), HashOf(token), Now.AddDays(-daysAgo).AddHours(-PasswordResetToken.ValidityHours));
            PasswordResetTokenSingleton.Instance.Add(resetToken);
            return resetToken;
        }

        [Fact]
        public async Task DeleteStaleAsync_ShouldDeleteSignUpsExpiredMoreThanTheRetentionAgo()
        {
            SeedPendingExpired("old@example.com", daysAgo: PendingRegistration.RetentionDays + 1);
            SeedPendingExpired("recent@example.com", daysAgo: PendingRegistration.RetentionDays - 1);

            var result = await _authCleanupService.DeleteStaleAsync(CancellationToken.None);

            Assert.Equal(1, result.PendingRegistrations);
            Assert.Equal("recent@example.com", Assert.Single(PendingRegistrationSingleton.Instance).Email.Value);
        }

        [Fact]
        public async Task DeleteStaleAsync_ShouldDeleteResetLinksExpiredOrUsedMoreThanTheRetentionAgo()
        {
            SeedResetExpired("expired-long-ago", daysAgo: PasswordResetToken.RetentionDays + 1);
            SeedResetExpired("expired-recently", daysAgo: PasswordResetToken.RetentionDays - 1);
            var usedLongAgo = PasswordResetToken.Create(new UserId(2), HashOf("used-long-ago"), Now.AddDays(-30));
            usedLongAgo.MarkUsed(Now.AddDays(-PasswordResetToken.RetentionDays - 1));
            PasswordResetTokenSingleton.Instance.Add(usedLongAgo);
            var usable = PasswordResetToken.Create(new UserId(3), HashOf("usable"), Now);
            PasswordResetTokenSingleton.Instance.Add(usable);

            var result = await _authCleanupService.DeleteStaleAsync(CancellationToken.None);

            Assert.Equal(2, result.PasswordResetTokens);
            Assert.Equal(
                [HashOf("expired-recently"), HashOf("usable")],
                PasswordResetTokenSingleton.Instance.Select(token => token.TokenHash).ToArray());
        }

        [Fact]
        public async Task DeleteStaleAsync_ShouldDeleteCooldownsOlderThanTheRetention()
        {
            EmailCooldownSingleton.Instance.Add(EmailCooldown.Create("old@example.com", EmailKind.Confirmation, Now - EmailCooldown.Retention - TimeSpan.FromMinutes(1)));
            EmailCooldownSingleton.Instance.Add(EmailCooldown.Create("recent@example.com", EmailKind.PasswordReset, Now - TimeSpan.FromHours(23)));

            var result = await _authCleanupService.DeleteStaleAsync(CancellationToken.None);

            Assert.Equal(1, result.EmailCooldowns);
            Assert.Equal("recent@example.com", Assert.Single(EmailCooldownSingleton.Instance).Email);
        }

        [Fact]
        public async Task DeleteStaleAsync_ShouldDeleteNothing_WhenEverythingIsRecent()
        {
            SeedPendingExpired("recent@example.com", daysAgo: 0);
            SeedResetExpired("recent", daysAgo: 0);
            EmailCooldownSingleton.Instance.Add(EmailCooldown.Create("recent@example.com", EmailKind.AccountExists, Now));

            var result = await _authCleanupService.DeleteStaleAsync(CancellationToken.None);

            Assert.Equal(0, result.Total);
        }
    }
}
