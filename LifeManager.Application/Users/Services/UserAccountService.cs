using LifeManager.Application.Auth.Emails;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Shared.Time;
using LifeManager.Application.Users.DTOs;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.Users.Services
{
    /// <summary>"My account" actions that touch the password or the sessions of the signed-in user.</summary>
    public class UserAccountService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IEmailSender emailSender,
        FrontendLinks frontendLinks,
        AuthService authService,
        TokenService tokenService,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IUserPreferencesRepository _userPreferencesRepository = userPreferencesRepository;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly FrontendLinks _frontendLinks = frontendLinks;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly AuthService _authService = authService;
        private readonly TokenService _tokenService = tokenService;

        /// <summary>Changes the password, ends every other session and tells the owner by e-mail.</summary>
        /// <param name="currentRefreshToken">The cookie of the session making the change, which stays signed in; every other session ends.</param>
        public async Task<Result> ChangePasswordAsync(UserId userId, ChangePasswordDto dto, string? currentRefreshToken, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return UserErrors.NotFound;

            // The current password first, so a wrong one is reported whatever the new one is.
            if (!_authService.VerifyPassword(dto.CurrentPassword ?? string.Empty, user.PasswordHash.Value))
                return UserErrors.WrongPassword;

            var plainPasswordResult = PlainPassword.Create(dto.NewPassword);
            if (!plainPasswordResult.IsSuccess)
                return plainPasswordResult.Error;

            var passwordHashResult = PasswordHash.Create(_authService.EncryptPassword(plainPasswordResult.Value.Value));
            if (!passwordHashResult.IsSuccess)
                return passwordHashResult.Error;

            RefreshTokenHash? keepTokenHash = null;
            if (!string.IsNullOrWhiteSpace(currentRefreshToken))
            {
                var tokenHashResult = _tokenService.HashOf(currentRefreshToken);
                if (!tokenHashResult.IsSuccess)
                    return tokenHashResult.Error;

                keepTokenHash = tokenHashResult.Value;
            }

            await _userRepository.ChangePasswordAsync(userId, passwordHashResult.Value, keepTokenHash, cancellationToken);

            // So the owner notices a change they didn't make. A failed send is logged by the sender; the change stands.
            var preferences = await _userPreferencesRepository.GetByUserIdAsync(userId, cancellationToken);
            await _emailSender.SendAsync(
                AuthEmailTemplates.PasswordChanged(user.Email, user.Name, preferences?.Language ?? Language.PTBR, _frontendLinks.ForgotPasswordUrl()),
                cancellationToken);

            return Result.Success();
        }

        /// <summary>
        /// Schedules the account's deletion <see cref="AccountDeletion.GraceDays"/> days from now, ends every session and
        /// tells the user by e-mail. Asking again during the grace period keeps the first date and sends nothing.
        /// </summary>
        public async Task<Result<AccountDeletionResponseDto>> RequestDeletionAsync(UserId userId, RequestAccountDeletionDto dto, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return UserErrors.NotFound;

            if (!_authService.VerifyPassword(dto.Password ?? string.Empty, user.PasswordHash.Value))
                return UserErrors.WrongPassword;

            var newlyScheduled = user.ScheduleDeletion(_timeProvider.GetUtcNow());
            var scheduled = await _userRepository.ScheduleDeletionAsync(userId, user.DeletionRequestedAt!.Value, user.DeletionScheduledFor!.Value, cancellationToken);

            if (!newlyScheduled || !scheduled)
            {
                // Already pending (or marked by a concurrent request): the stored date stands, and its e-mail already went.
                var stored = await _userRepository.GetByIdAsync(userId, cancellationToken);
                return new AccountDeletionResponseDto((stored ?? user).DeletionScheduledFor!.Value);
            }

            var preferences = await _userPreferencesRepository.GetByUserIdAsync(userId, cancellationToken);
            // A failed send is logged by the sender; the deletion stands either way.
            await _emailSender.SendAsync(
                AuthEmailTemplates.AccountDeletionScheduled(
                    user.Email, user.Name, preferences?.Language ?? Language.PTBR,
                    _appClock.DateOf(user.DeletionScheduledFor.Value), _frontendLinks.LoginUrl()),
                cancellationToken);

            return new AccountDeletionResponseDto(user.DeletionScheduledFor.Value);
        }

        /// <summary>Ends every session of the user, this one included. Access tokens already issued last until they expire (≤ 15 min).</summary>
        public async Task<Result> LogoutAllAsync(UserId userId, CancellationToken cancellationToken)
        {
            await _refreshTokenRepository.RevokeAllActiveByUserIdAsync(userId, cancellationToken);

            return Result.Success();
        }
    }
}
