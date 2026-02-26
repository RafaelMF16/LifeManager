using LifeManager.Application.Auth;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;

namespace LifeManager.Application.Users
{
    public class UserService(
        IUserRepository userRepository,
        AuthService authService)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AuthService _authService = authService;

        public User AddUser(UserDto userDto)
        {
            UserPassword.ValidatePassword(userDto.UserPassword);

            var hashedPassword = _authService.EncryptPassword(userDto.UserPassword);

            var user = User.Create(userDto.UserId, userDto.Name, userDto.Email, hashedPassword);
            
            _userRepository.Add(user);

            return user;
        }
    }
}