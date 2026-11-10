using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Errors;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    /// <summary>"My account" actions that touch the password or the sessions of the signed-in user.</summary>
    public class UserAccountService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        AuthService authService,
        TokenService tokenService)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly AuthService _authService = authService;
        private readonly TokenService _tokenService = tokenService;

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

            return Result.Success();
        }

        /// <summary>Ends every session of the user, this one included. Access tokens already issued last until they expire (≤ 15 min).</summary>
        public async Task<Result> LogoutAllAsync(UserId userId, CancellationToken cancellationToken)
        {
            await _refreshTokenRepository.RevokeAllActiveByUserIdAsync(userId, cancellationToken);

            return Result.Success();
        }
    }
}
