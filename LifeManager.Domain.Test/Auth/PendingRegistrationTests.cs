using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Domain.Test.Auth
{
    public class PendingRegistrationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private static readonly EmailTokenHash FirstHash = EmailTokenHash.FromToken("first").Value!;
        private static readonly EmailTokenHash SecondHash = EmailTokenHash.FromToken("second").Value!;

        [Fact]
        public void Create_ShouldReturnPendingRegistration_WhenDataIsValid()
        {
            var result = PendingRegistration.Create("Ana Silva", "ana@example.com", Language.EN, Theme.Dark, FirstHash, Now);

            Assert.True(result.IsSuccess);
            var pending = result.Value;
            Assert.Equal("Ana Silva", pending.Name.Value);
            Assert.Equal("ana@example.com", pending.Email.Value);
            Assert.Equal(Language.EN, pending.Language);
            Assert.Equal(Theme.Dark, pending.Theme);
            Assert.Equal(FirstHash, pending.TokenHash);
            Assert.Equal(Now, pending.CreatedAt);
            Assert.Equal(Now.AddHours(PendingRegistration.ValidityHours), pending.ExpiresAt);
        }

        [Fact]
        public void Create_ShouldReturnNameError_WhenNameIsEmpty()
        {
            var result = PendingRegistration.Create(" ", "ana@example.com", null, null, FirstHash, Now);

            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnEmailError_WhenEmailIsInvalid()
        {
            var result = PendingRegistration.Create("Ana", "ana", null, null, FirstHash, Now);

            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
        }

        [Fact]
        public void Create_ShouldFallBackToPortugueseAndLight_WhenPreferencesAreMissingOrUndefined()
        {
            var missing = PendingRegistration.Create("Ana", "ana@example.com", null, null, FirstHash, Now).Value!;
            var undefined = PendingRegistration.Create("Ana", "ana@example.com", (Language)99, (Theme)99, FirstHash, Now).Value!;

            Assert.Equal((Language.PTBR, Theme.Light), (missing.Language, missing.Theme));
            Assert.Equal((Language.PTBR, Theme.Light), (undefined.Language, undefined.Theme));
        }

        [Fact]
        public void Renew_ShouldReplaceDataTokenAndValidity_WhenSignedUpAgain()
        {
            var pending = PendingRegistration.Create("Ana", "ana@example.com", Language.PTBR, Theme.Light, FirstHash, Now).Value!;
            var later = Now.AddHours(30);

            var result = pending.Renew("Ana Souza", Language.EN, Theme.Dark, SecondHash, later);

            Assert.True(result.IsSuccess);
            Assert.Equal("Ana Souza", pending.Name.Value);
            Assert.Equal("ana@example.com", pending.Email.Value);
            Assert.Equal((Language.EN, Theme.Dark), (pending.Language, pending.Theme));
            Assert.Equal(SecondHash, pending.TokenHash);
            Assert.Equal(later.AddHours(PendingRegistration.ValidityHours), pending.ExpiresAt);
        }

        [Fact]
        public void Renew_ShouldKeepEverything_WhenNameIsInvalid()
        {
            var pending = PendingRegistration.Create("Ana", "ana@example.com", null, null, FirstHash, Now).Value!;

            var result = pending.Renew("", null, null, SecondHash, Now.AddHours(1));

            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
            Assert.Equal("Ana", pending.Name.Value);
            Assert.Equal(FirstHash, pending.TokenHash);
        }

        [Fact]
        public void IsExpired_ShouldBeFalseBeforeAndTrueFromExpiresAt()
        {
            var pending = PendingRegistration.Create("Ana", "ana@example.com", null, null, FirstHash, Now).Value!;

            Assert.False(pending.IsExpired(pending.ExpiresAt.AddSeconds(-1)));
            Assert.True(pending.IsExpired(pending.ExpiresAt));
        }
    }
}
