using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users;

namespace LifeManager.Domain.Test.Users
{
    public class UserPasswordTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserPasswordIsNull()
        {
            const string errorMessageExpected = $"{nameof(UserPassword)} is required";
            var exception = Assert.Throws<DomainException>(() => UserPassword.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserPasswordIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(UserPassword)} is required";
            var exception = Assert.Throws<DomainException>(() => UserPassword.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserPasswordIsGreaterThan50()
        {
            const string errorMessageExpected = $"{nameof(UserPassword)} cannot be longer than 50 characters";
            const string longPassword = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var exception = Assert.Throws<DomainException>(() => UserPassword.Create(longPassword));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserPasswordIsLessThan8()
        {
            const string errorMessageExpected = $"{nameof(UserPassword)} must be at least 8 characters long";
            const string longPassword = "aaaa";
            var exception = Assert.Throws<DomainException>(() => UserPassword.Create(longPassword));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnUserPassword_WhenUserPasswordIsValid()
        {
            const string validPassword = "password";
            var password = UserPassword.Create(validPassword);

            Assert.NotNull(password);
            Assert.IsType<UserPassword>(password);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = UserPassword.Create(password);
            var valueTwo = UserPassword.Create(password);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = UserPassword.Create(password);
            var valueTwo = UserPassword.Create(password);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}