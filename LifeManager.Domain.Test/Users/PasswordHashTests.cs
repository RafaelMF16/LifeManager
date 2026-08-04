using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class PasswordHashTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsNull()
        {
            const string errorMessageExpected = $"{nameof(PasswordHash)} is required";
            var exception = Assert.Throws<DomainException>(() => PasswordHash.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(PasswordHash)} is required";
            var exception = Assert.Throws<DomainException>(() => PasswordHash.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnPasswordHash_WhenValueIsValid()
        {
            const string hash = "$2a$10$abcdefghijklmnopqrstuvwxyz012345";
            var passwordHashResult = PasswordHash.Create(hash);
            var passwordHash = passwordHashResult.Value;

            Assert.NotNull(passwordHashResult.Value);
            Assert.IsType<PasswordHash>(passwordHashResult.Value);
            Assert.Equal(hash, passwordHash.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = PasswordHash.Create(hash);
            var valueTwo = PasswordHash.Create(hash);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string hash = "hash";
            var valueOne = PasswordHash.Create(hash);
            var valueTwo = PasswordHash.Create(hash);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
