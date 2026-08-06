using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class PasswordHashTests
    {
        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsNull()
        {
            var result = PasswordHash.Create(null!);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PasswordHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsEmpty()
        {
            var result = PasswordHash.Create(string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PasswordHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnPasswordHash_WhenValueIsValid()
        {
            const string hash = "$2a$10$abcdefghijklmnopqrstuvwxyz012345";
            var passwordHashResult = PasswordHash.Create(hash);
            var passwordHash = passwordHashResult.Value;

            Assert.True(passwordHashResult.IsSuccess);
            Assert.IsType<PasswordHash>(passwordHash);
            Assert.Equal(hash, passwordHash.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = PasswordHash.Create(hash).Value;
            var valueTwo = PasswordHash.Create(hash).Value;
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = PasswordHash.Create(hash).Value;
            var valueTwo = PasswordHash.Create(hash).Value;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
