using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.ValueObjects;

namespace LifeManager.Domain.Test.Auth
{
    public class EmailTokenHashTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FromToken_ShouldReturnInvalidEmailToken_WhenTokenIsNullOrWhiteSpace(string? token)
        {
            var result = EmailTokenHash.FromToken(token);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidEmailToken, result.Error);
        }

        [Fact]
        public void FromToken_ShouldReturnTheSha256InLowercaseHex_WhenTokenIsGiven()
        {
            var result = EmailTokenHash.FromToken("abc");

            Assert.True(result.IsSuccess);
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Value.Value);
        }

        [Fact]
        public void FromToken_ShouldReturnEqualHashes_WhenTokensAreEqual()
        {
            var first = EmailTokenHash.FromToken("same-token").Value;
            var second = EmailTokenHash.FromToken("same-token").Value;

            Assert.Equal(first, second);
            Assert.Equal(first!.GetHashCode(), second!.GetHashCode());
        }

        [Fact]
        public void FromToken_ShouldReturnDifferentHashes_WhenTokensDiffer()
        {
            var first = EmailTokenHash.FromToken("token-a").Value;
            var second = EmailTokenHash.FromToken("token-b").Value;

            Assert.NotEqual(first, second);
        }
    }
}
