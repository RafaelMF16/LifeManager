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

        public async Task<Result<UserResponseDto>> AddUserAsync(UserDto userDto, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(userDto.Email);
            if (!emailResult.IsSuccess)
                return emailResult.Error;

            if (await _userRepository.ExistsByEmailAsync(emailResult.Value, cancellationToken))
                return UserErrors.EmailRegistered;

            var plainPasswordResult = PlainPassword.Create(userDto.UserPassword);
            if (!plainPasswordResult.IsSuccess)
                return plainPasswordResult.Error;

            var hashedPassword = _authService.EncryptPassword(plainPasswordResult.Value.Value);
            var userResult = User.Create(userDto.Name, userDto.Email, hashedPassword);
            if (!userResult.IsSuccess)
                return userResult.Error;

            var user = await _userRepository.AddAsync(userResult.Value, cancellationToken);

            return new UserResponseDto(user.Id!.Value, user.Name.Value, user.Email.Value);
        }

        public async Task<Result<LoginResponseDto>> AuthenticateUserAsync(LoginDto loginDto, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(loginDto.Email);
            if (!emailResult.IsSuccess)
                return UserErrors.InvalidCredentials;

            var user = await _userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
            if (user is null || !_authService.VerifyPassword(loginDto.Password, user.PasswordHash.Value))
                return UserErrors.InvalidCredentials;

            return await _tokenService.GenerateTokensAsync(user.Id!.Value, cancellationToken);
        }
    }
}
