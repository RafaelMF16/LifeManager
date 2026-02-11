using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class UserNameTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserNameIsNull()
        {
            const string errorMessageExpected = $"{nameof(UserName)} is required";
            var exception = Assert.Throws<DomainException>(() => UserName.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserNameIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(UserName)} is required";
            var exception = Assert.Throws<DomainException>(() => UserName.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenUserNameGreaterThan100()
        {
            const string errorMessageExpected = $"{nameof(UserName)} cannot be longer than 100 characters";
            const string longUserName = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var exception = Assert.Throws<DomainException>(() => UserName.Create(longUserName));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnUserName_WhenUserNameIsValid()
        {
            const string validUserName = "userName";
            var userName = UserName.Create(validUserName);

            Assert.NotNull(userName);
            Assert.IsType<UserName>(userName);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string userName = "userName";
            var valueOne = UserName.Create(userName);
            var valueTwo = UserName.Create(userName);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string userName = "userName";
            var valueOne = UserName.Create(userName);
            var valueTwo = UserName.Create(userName);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}