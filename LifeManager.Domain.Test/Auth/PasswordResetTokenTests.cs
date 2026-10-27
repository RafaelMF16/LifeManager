using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Auth
{
    public class PasswordResetTokenTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private static readonly EmailTokenHash Hash = EmailTokenHash.FromToken("token").Value!;

        [Fact]
        public void Create_ShouldExpireAfterTheValidity()
        {
            var token = PasswordResetToken.Create(new UserId(7), Hash, Now);

            Assert.Equal(new UserId(7), token.UserId);
            Assert.Equal(Hash, token.TokenHash);
            Assert.Equal(Now, token.CreatedAt);
            Assert.Equal(Now.AddHours(PasswordResetToken.ValidityHours), token.ExpiresAt);
            Assert.Null(token.UsedAt);
        }

        [Fact]
        public void IsUsable_ShouldBeTrueUntilItExpires()
        {
            var token = PasswordResetToken.Create(new UserId(7), Hash, Now);

            Assert.True(token.IsUsable(token.ExpiresAt.AddSeconds(-1)));
            Assert.False(token.IsUsable(token.ExpiresAt));
        }

        [Fact]
        public void IsUsable_ShouldBeFalse_OnceUsed()
        {
            var token = PasswordResetToken.Create(new UserId(7), Hash, Now);

            token.MarkUsed(Now.AddMinutes(5));

            Assert.Equal(Now.AddMinutes(5), token.UsedAt);
            Assert.False(token.IsUsable(Now.AddMinutes(6)));
        }
    }
}
