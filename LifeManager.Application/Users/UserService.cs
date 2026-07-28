using LifeManager.Application.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;

namespace LifeManager.Application.Users
{
    public class UserService(
        IUserRepository userRepository,
        AuthService authService,
        IRefreshTokenRepository refreshTokenRepository)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly AuthService _authService = authService;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

        public User AddUser(UserDto userDto)
        {
            UserPassword.ValidatePassword(userDto.UserPassword);

            var hashedPassword = _authService.EncryptPassword(userDto.UserPassword);

            var user = User.Create(userDto.Name, userDto.Email, hashedPassword);
            
            _userRepository.Add(user);

            return user;
        }

        public LoginResponseDto? AuthenticateUser(string email, string password)
        {
            var user = _userRepository.GetUserByEmail(email);

            if (user == null || !_authService.VerifyPassword(password, user.Password.Value))
                return null;

            var token = _refreshTokenRepository.GetValidTokenByUserId(user.Id!.Value);
            if (token is not null)
            {
                RefreshTokenRevoked.RevokeToken(token.IsRevoked);
                _refreshTokenRepository.UpdateRevoked(token);
            }

            return _authService.GenerateTokens(user.Id.Value);
        }
    }
}