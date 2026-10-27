using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;

namespace LifeManager.Domain.Test.Auth
{
    public class EmailCooldownTests
    {
        private static readonly DateTimeOffset SentAt = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CanSendAgain_ShouldBeFalse_WhenLessThanTheCooldownPassed()
        {
            var cooldown = EmailCooldown.Create("ana@example.com", EmailKind.Confirmation, SentAt);

            Assert.False(cooldown.CanSendAgain(SentAt.AddSeconds(59)));
        }

        [Fact]
        public void CanSendAgain_ShouldBeTrue_WhenTheCooldownPassed()
        {
            var cooldown = EmailCooldown.Create("ana@example.com", EmailKind.Confirmation, SentAt);

            Assert.True(cooldown.CanSendAgain(SentAt.Add(EmailCooldown.Duration)));
        }

        [Fact]
        public void Create_ShouldKeyByTheLowercaseEmail_WhenEmailHasUppercase()
        {
            var cooldown = EmailCooldown.Create("Ana@Example.com", EmailKind.PasswordReset, SentAt);

            Assert.Equal("ana@example.com", cooldown.Email);
            Assert.Equal(EmailKind.PasswordReset, cooldown.Kind);
            Assert.Equal(SentAt, cooldown.LastSentAt);
        }

        [Fact]
        public void MarkSent_ShouldMoveLastSentAt()
        {
            var cooldown = EmailCooldown.Create("ana@example.com", EmailKind.AccountExists, SentAt);

            cooldown.MarkSent(SentAt.AddMinutes(5));

            Assert.Equal(SentAt.AddMinutes(5), cooldown.LastSentAt);
        }
    }
}
