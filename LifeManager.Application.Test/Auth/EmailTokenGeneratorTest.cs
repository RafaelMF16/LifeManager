using LifeManager.Application.Auth.Services;
using LifeManager.Domain.Auth.ValueObjects;

namespace LifeManager.Application.Test.Auth
{
    public class EmailTokenGeneratorTest
    {
        [Fact]
        public void Generate_ShouldReturnTheHashOfTheToken()
        {
            var (token, hash) = EmailTokenGenerator.Generate();

            Assert.Equal(EmailTokenHash.FromToken(token).Value, hash);
        }

        [Fact]
        public void Generate_ShouldReturnAnUrlSafeTokenOf32Bytes()
        {
            var (token, _) = EmailTokenGenerator.Generate();

            Assert.Equal(43, token.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
        }

        [Fact]
        public void Generate_ShouldNotRepeatTokens()
        {
            var tokens = Enumerable.Range(0, 1000).Select(_ => EmailTokenGenerator.Generate().Token).ToHashSet();

            Assert.Equal(1000, tokens.Count);
        }
    }
}
