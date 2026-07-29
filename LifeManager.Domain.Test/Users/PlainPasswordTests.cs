using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class PlainPasswordTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenPasswordIsNull()
        {
            const string errorMessageExpected = $"{nameof(PlainPassword)} is required";
            var exception = Assert.Throws<DomainException>(() => PlainPassword.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenPasswordIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(PlainPassword)} is required";
            var exception = Assert.Throws<DomainException>(() => PlainPassword.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenPasswordIsGreaterThan50()
        {
            const string errorMessageExpected = $"{nameof(PlainPassword)} cannot be longer than 50 characters";
            const string longPassword = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var exception = Assert.Throws<DomainException>(() => PlainPassword.Create(longPassword));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenPasswordIsLessThan8()
        {
            const string errorMessageExpected = $"{nameof(PlainPassword)} must be at least 8 characters long";
            const string shortPassword = "aaaa";
            var exception = Assert.Throws<DomainException>(() => PlainPassword.Create(shortPassword));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnPlainPassword_WhenPasswordIsValid()
        {
            const string validPassword = "password";
            var password = PlainPassword.Create(validPassword);

            Assert.NotNull(password);
            Assert.IsType<PlainPassword>(password);
            Assert.Equal(validPassword, password.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = PlainPassword.Create(password);
            var valueTwo = PlainPassword.Create(password);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string password = "password";
            var valueOne = PlainPassword.Create(password);
            var valueTwo = PlainPassword.Create(password);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
