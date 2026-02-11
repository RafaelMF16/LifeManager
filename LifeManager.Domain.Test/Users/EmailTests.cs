using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users.ValueObjects;
using System;

namespace LifeManager.Domain.Test.Users
{
    public class EmailTests
    {
        [Theory]
        [InlineData("@email")]
        [InlineData("email@")]
        [InlineData("email")]
        public void Create_ShouldThrowDomainException_WhenEmailIsInvalid(string email)
        {
            const string errorMessageExpected = $"{nameof(Email)} is invalid";
            var exception = Assert.Throws<DomainException>(() => Email.Create(email));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenEmailIsNull()
        {
            const string errorMessageExpected = $"{nameof(Email)} is required";
            var exception = Assert.Throws<DomainException>(() => Email.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenEmailIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(Email)} is required";
            var exception = Assert.Throws<DomainException>(() => Email.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnEmail_WhenEmailIsValid()
        {
            const string validEmail = "email@email.com";
            var email = Email.Create(validEmail);

            Assert.NotNull(email);
            Assert.IsType<Email>(email);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string email = "email@email.com";
            var valueOne = Email.Create(email);
            var valueTwo = Email.Create(email);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string email = "email@email.com";
            var valueOne = Email.Create(email);
            var valueTwo = Email.Create(email);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}