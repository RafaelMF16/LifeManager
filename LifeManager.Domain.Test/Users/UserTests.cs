using LifeManager.Domain.Users;

namespace LifeManager.Domain.Test.Users
{
    public class UserTests
    {
        [Fact]
        public void Create_ShouldReturnUser_WhenUserIsValid()
        {
            var id = 1;
            var userName = "test";
            var email = "r@email.com";
            var password = "password";
            var user = User.Create(id, userName, email, password);

            Assert.NotNull(user);
            Assert.IsType<User>(user);
        }
    }
}