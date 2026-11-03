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

        public async Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(UserId userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return UserErrors.NotFound;

            return new CurrentUserResponseDto(user.Name.Value);
        }

        public async Task<Result<LoginResponseDto>> AuthenticateUserAsync(LoginDto loginDto, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(loginDto.Email);
            if (!emailResult.IsSuccess)
                return UserErrors.InvalidCredentials;

            var user = await _userRepository.GetByEmailAsync(emailResult.Value, cancellationToken);
            if (!_authService.VerifyPassword(loginDto.Password, user?.PasswordHash.Value) || user is null)
                return UserErrors.InvalidCredentials;

            // The only moment the plain password is at hand, so accounts move to a raised cost as their owners log in.
            if (_authService.NeedsRehash(user.PasswordHash.Value)
                && PasswordHash.Create(_authService.EncryptPassword(loginDto.Password)) is { IsSuccess: true } rehashed)
                await _userRepository.UpdatePasswordHashAsync(user.Id!, rehashed.Value, cancellationToken);

            return await _tokenService.GenerateTokensAsync(user.Id!.Value, cancellationToken);
        }
    }
}
