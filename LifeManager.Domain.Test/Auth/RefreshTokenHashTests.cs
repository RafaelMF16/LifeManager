using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Test.Auth
{
    public class RefreshTokenHashTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsNull()
        {
            const string errorMessageExpected = $"{nameof(RefreshTokenHash)} is required";
            var exception = Assert.Throws<DomainException>(() => RefreshTokenHash.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(RefreshTokenHash)} is required";
            var exception = Assert.Throws<DomainException>(() => RefreshTokenHash.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
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
