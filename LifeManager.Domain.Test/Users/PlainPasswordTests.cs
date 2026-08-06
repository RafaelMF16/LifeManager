using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class PlainPasswordTests
    {
        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordIsNull()
        {
            var result = PlainPassword.Create(null!);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordIsEmpty()
        {
            var result = PlainPassword.Create(string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordIsGreaterThan50()
        {
            const string longPassword = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var result = PlainPassword.Create(longPassword);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordIsLessThan8()
        {
            const string shortPassword = "aaaa";
            var result = PlainPassword.Create(shortPassword);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PlainPasswordTooShort, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnPlainPassword_WhenPasswordIsValid()
        {
            const string validPassword = "password";
            var passwordResult = PlainPassword.Create(validPassword);
            var password = passwordResult.Value;

            Assert.True(passwordResult.IsSuccess);
            Assert.IsType<PlainPassword>(password);
            Assert.Equal(validPassword, password.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = PlainPassword.Create(password).Value;
            var valueTwo = PlainPassword.Create(password).Value;
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = PlainPassword.Create(password).Value;
            var valueTwo = PlainPassword.Create(password).Value;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
