using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    public class UserService(
        IUserRepository userRepository,
        AuthService authService,
        TokenService tokenService)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AuthService _authService = authService;
        private readonly TokenService _tokenService = tokenService;

        public Result<UserResponseDto> AddUser(UserDto userDto)
        {
            var existingUser = _userRepository.GetUserByEmail(userDto.Email);
            if (existingUser is not null)
                return UserErrors.EmailRegistered;

            var plainPasswordResult = PlainPassword.Create(userDto.UserPassword);
            if (!plainPasswordResult.IsSuccess)
                return plainPasswordResult.Error;

            var plainPassword = plainPasswordResult.Value;
            var hashedPassword = _authService.EncryptPassword(plainPassword.Value);

            var userResult = User.Create(userDto.Name, userDto.Email, hashedPassword);
            if (!userResult.IsSuccess)
                return userResult.Error;

            var user = userResult.Value;
            _userRepository.Add(user);

            return new UserResponseDto(user.Id!.Value, user.Name.Value, user.Email.Value);
        }

        public LoginResponseDto? AuthenticateUser(string email, string password)
        {
            var user = _userRepository.GetUserByEmail(email);

            if (user == null || !_authService.VerifyPassword(password, user.PasswordHash.Value))
                return null;

            return _tokenService.GenerateTokens(user.Id!.Value);
        }
    }
}