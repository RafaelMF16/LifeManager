using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;

namespace LifeManager.Domain.Test.Users
{
    public class UserTests
    {
        [Fact]
        public void Create_ShouldReturnUser_WhenUserIsValid()
        {
            var userName = "test";
            var email = "r@email.com";
            var passwordHash = "$2a$10$abcdefghijklmnopqrstuvwxyz012345";
            var user = User.Create(userName, email, passwordHash).Value;

            Assert.NotNull(user);
            Assert.IsType<User>(user);
            Assert.Null(user.Id);
            Assert.Equal(userName, user.Name.Value);
            Assert.Equal(email, user.Email.Value);
            Assert.Equal(passwordHash, user.PasswordHash.Value);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenUserNameIsInvalid()
        {
            var result = User.Create(string.Empty, "r@email.com", "hash");

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.UserNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenEmailIsInvalid()
        {
            var result = User.Create("test", "invalid-email", "hash");

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.EmailIsInvalid, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenPasswordHashIsInvalid()
        {
            var result = User.Create("test", "r@email.com", string.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(UserErrors.PasswordHashIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenUserHasNoIdYet()
        {
            var user = User.Create("test", "r@email.com", "hash").Value;
            Assert.NotNull(user);

            user.AssignId(10);

            Assert.NotNull(user.Id);
            Assert.Equal(10, user.Id!.Value);
        }
    }
}
