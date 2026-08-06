using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Users
{
    public class EmailTests
    {
        [Theory]
        [InlineData("@email")]
        [InlineData("email@")]
        [InlineData("email")]
        public void Create_ShouldReturnFailure_WhenEmailIsInvalid(string email)
        {
            var result = Email.Create(email);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenEmailIsNull()
        {
            var result = Email.Create(null!);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenEmailIsEmpty()
        {
            var result = Email.Create(string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnEmail_WhenEmailIsValid()
        {
            const string validEmail = "email@email.com";
            var result = Email.Create(validEmail);

            Assert.True(result.IsSuccess);
            Assert.IsType<Email>(result.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string email = "email@email.com";
            var valueOne = Email.Create(email).Value;
            var valueTwo = Email.Create(email).Value;
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string email = "email@email.com";
            var valueOne = Email.Create(email).Value;
            var valueTwo = Email.Create(email).Value;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
