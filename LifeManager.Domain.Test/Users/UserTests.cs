using LifeManager.Domain.Users;

namespace LifeManager.Domain.Test.Users
{
    public class UserTests
    {
        [Fact]
        public void Create_ShouldReturnUser_WhenUserIsValid()
        {
            var userName = "test";
            var email = "r@email.com";
            var password = "password";
            var user = User.Create(userName, email, password);

            Assert.NotNull(user);
            Assert.IsType<User>(user);
            Assert.Null(user.Id);
            Assert.Equal(userName, user.Name.Value);
            Assert.Equal(email, user.Email.Value);
            Assert.Equal(password, user.PasswordHash.Value);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenUserHasNoIdYet()
        {
            var user = User.Create("test", "r@email.com", "password");

            user.AssignId(10);

            Assert.NotNull(user.Id);
            Assert.Equal(10, user.Id!.Value);
        }
    }
}