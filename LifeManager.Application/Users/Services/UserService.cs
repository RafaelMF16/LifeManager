using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    public class UserService(
        IUserRepository userRepository,
        ILoginAttemptRepository loginAttemptRepository,
        AuthService authService,
        TokenService tokenService,
        TimeProvider timeProvider)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly ILoginAttemptRepository _loginAttemptRepository = loginAttemptRepository;
        private readonly AuthService _authService = authService;
        private readonly TokenService _tokenService = tokenService;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<CurrentUserResponseDto>> GetCurrentUserAsync(UserId userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return UserErrors.NotFound;

            return new CurrentUserResponseDto(user.Name.Value, user.Email.Value);
        }

        public async Task<Result<UpdatedUserNameResponseDto>> RenameAsync(UserId userId, UpdateCurrentUserDto dto, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return UserErrors.NotFound;

            var renameResult = user.Rename(dto.Name);
            if (!renameResult.IsSuccess)
                return renameResult.Error;

            await _userRepository.UpdateNameAsync(userId, user.Name, cancellationToken);

            return new UpdatedUserNameResponseDto(user.Name.Value);
        }

        /// <param name="ipAddress">The client's address; with the e-mail, the pair whose wrong passwords are counted.</param>
        public async Task<Result<LoginResponseDto>> AuthenticateUserAsync(LoginDto loginDto, string ipAddress, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(loginDto.Email);
            if (!emailResult.IsSuccess)
                return UserErrors.InvalidCredentials;

            var email = emailResult.Value;
            var now = _timeProvider.GetUtcNow();

            // Before the password: while blocked, not even the right one gets in, so guessing stops for the whole window.
            var attempt = await _loginAttemptRepository.GetAsync(email, ipAddress, cancellationToken);
            if (attempt is not null && attempt.IsBlocked(now))
                return AuthErrors.TooManyRequests;

            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (!_authService.VerifyPassword(loginDto.Password, user?.PasswordHash.Value) || user is null)
            {
                await _loginAttemptRepository.RecordFailureAsync(email, ipAddress, now, cancellationToken);
                return UserErrors.InvalidCredentials;
            }

            if (attempt is not null)
                await _loginAttemptRepository.ClearAsync(email, ipAddress, cancellationToken);

            // The only moment the plain password is at hand, so accounts move to a raised cost as their owners log in.
            if (_authService.NeedsRehash(user.PasswordHash.Value)
                && PasswordHash.Create(_authService.EncryptPassword(loginDto.Password)) is { IsSuccess: true } rehashed)
                await _userRepository.UpdatePasswordHashAsync(user.Id!, rehashed.Value, cancellationToken);

            // Signing in during the grace period keeps the account. If the deletion job got the row first, there's no
            // account left to sign in to.
            var deletionCancelled = false;
            if (user.IsDeletionPending)
            {
                if (!await _userRepository.CancelDeletionAsync(user.Id!, cancellationToken)
                    && await _userRepository.GetByIdAsync(user.Id!, cancellationToken) is null)
                    return UserErrors.InvalidCredentials;

                deletionCancelled = true;
            }

            var tokensResult = await _tokenService.GenerateTokensAsync(user.Id!.Value, cancellationToken);

            return tokensResult.Map(tokens => tokens with { DeletionCancelled = deletionCancelled });
        }
    }
}
