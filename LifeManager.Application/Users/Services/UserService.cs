using LifeManager.Application.Auth;
using LifeManager.Application.Users.DTOs;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    public class UserService(
        IUserRepository userRepository,
        AuthService authService,
        IRefreshTokenRepository refreshTokenRepository)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AuthService _authService = authService;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

        public UserResponseDto AddUser(UserDto userDto)
        {
            var existingUser = _userRepository.GetUserByEmail(userDto.Email);
            if (existingUser is not null)
                throw new DomainException("Something went wrong! Check the email or password.");

            var plainPassword = PlainPassword.Create(userDto.UserPassword);

            var hashedPassword = _authService.EncryptPassword(plainPassword.Value);

            var user = User.Create(userDto.Name, userDto.Email, hashedPassword);
            
            _userRepository.Add(user);

            return new UserResponseDto(user.Id!.Value, user.Name.Value, user.Email.Value);
        }

        public LoginResponseDto? AuthenticateUser(string email, string password)
        {
            var user = _userRepository.GetUserByEmail(email);

            if (user == null || !_authService.VerifyPassword(password, user.PasswordHash.Value))
                return null;

            var token = _refreshTokenRepository.GetValidTokenByUserId(user.Id!.Value);
            if (token is not null)
            {
                token.RevokeToken();
                _refreshTokenRepository.UpdateRevoked(token);
            }

            return _authService.GenerateTokens(user.Id.Value);
        }
    }
}