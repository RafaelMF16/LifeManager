using LifeManager.Domain.Auth.ValueObjects;

namespace LifeManager.Domain.Test.Auth
{
    public class RefreshTokenRevokedTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Create_ShouldReturnRefreshTokenRevoked_WithGivenValue(bool value)
        {
            var refreshTokenRevoked = RefreshTokenRevoked.Create(value);

            Assert.NotNull(refreshTokenRevoked);
            Assert.IsType<RefreshTokenRevoked>(refreshTokenRevoked);
            Assert.Equal(value, refreshTokenRevoked.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = RefreshTokenRevoked.Create(true);
            var valueTwo = RefreshTokenRevoked.Create(true);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = RefreshTokenRevoked.Create(true);
            var valueTwo = RefreshTokenRevoked.Create(true);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
