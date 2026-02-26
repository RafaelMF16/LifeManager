using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Users
{
    public class UserServiceTest : BaseTest
    {
        private readonly UserService _userService;

        public UserServiceTest()
        {
            _userService = ServiceProvider.GetRequiredService<UserService>();

            UserSingleton.Instance.Clear();
        }

        [Fact]
        public void Add_ShouldAddUser_WhenUserIsValid()
        {
            var id = 1;
            var name = "name";
            var email = "email@email.com";
            var password = "password";
            var userDto = new UserDto(id, email, name, password);
            var newUser = _userService.AddUser(userDto);

            Assert.Contains(UserSingleton.Instance, user => user == newUser);
        }
    }
}