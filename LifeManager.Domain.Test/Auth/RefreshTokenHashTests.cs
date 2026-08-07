using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.ValueObjects;

namespace LifeManager.Domain.Test.Auth
{
    public class RefreshTokenHashTests
    {
        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsNull()
        {
            var result = RefreshTokenHash.Create(null!);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.RefreshTokenHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsEmpty()
        {
            var result = RefreshTokenHash.Create(string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.RefreshTokenHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnRefreshTokenHash_WhenValueIsValid()
        {
            const string hash = "hash";
            var result = RefreshTokenHash.Create(hash);
            var tokenHash = result.Value;

            Assert.NotNull(tokenHash);
            Assert.IsType<RefreshTokenHash>(tokenHash);
            Assert.Equal(hash, tokenHash.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = RefreshTokenHash.Create(hash);
            var valueTwo = RefreshTokenHash.Create(hash);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = RefreshTokenHash.Create(hash);
            var valueTwo = RefreshTokenHash.Create(hash);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
