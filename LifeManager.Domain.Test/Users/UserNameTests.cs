using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class UserNameTests
    {
        [Fact]
        public void Create_ShouldReturnFailure_WhenUserNameIsNull()
        {
            var result = UserName.Create(null!);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenUserNameIsEmpty()
        {
            var result = UserName.Create(string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenUserNameGreaterThan100()
        {
            const string longUserName = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var result = UserName.Create(longUserName);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnUserName_WhenUserNameIsValid()
        {
            const string validUserName = "userName";
            var result = UserName.Create(validUserName);

            Assert.True(result.IsSuccess);
            Assert.IsType<UserName>(result.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string userName = "userName";
            var valueOne = UserName.Create(userName).Value;
            var valueTwo = UserName.Create(userName).Value;
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string userName = "userName";
            var valueOne = UserName.Create(userName).Value;
            var valueTwo = UserName.Create(userName).Value;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
