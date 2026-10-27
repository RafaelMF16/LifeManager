using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Emails;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// Password recovery by e-mail: the request (<see cref="RequestAsync"/>) never tells whether the e-mail has an
    /// account, and the link sets a new password once, ending every open session.
    /// </summary>
    public class PasswordResetService(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IEmailCooldownRepository emailCooldownRepository,
        IEmailSender emailSender,
        FrontendLinks frontendLinks,
        AuthService authService,
        TimeProvider timeProvider)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUserPreferencesRepository _userPreferencesRepository = userPreferencesRepository;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = passwordResetTokenRepository;
        private readonly IEmailCooldownRepository _emailCooldownRepository = emailCooldownRepository;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly FrontendLinks _frontendLinks = frontendLinks;
        private readonly AuthService _authService = authService;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// An e-mail with an account gets a new link (replacing the ones not used yet); any other e-mail gets nothing.
        /// Inside the cooldown nothing changes, so the link already sent stays valid. Always succeeds for a valid e-mail.
        /// </summary>
        public async Task<Result> RequestAsync(ForgotPasswordDto forgotPasswordDto, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(forgotPasswordDto.Email);
            if (!emailResult.IsSuccess)
                return emailResult.Error;

            var email = emailResult.Value;
            var now = _timeProvider.GetUtcNow();

            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (user is null)
                return Result.Success();

            if (!await _emailCooldownRepository.TryReserveAsync(email, EmailKind.PasswordReset, now, cancellationToken))
                return Result.Success();

            var (token, tokenHash) = EmailTokenGenerator.Generate();
            await _passwordResetTokenRepository.AddReplacingUnusedAsync(PasswordResetToken.Create(user.Id!, tokenHash, now), cancellationToken);

            var preferences = await _userPreferencesRepository.GetByUserIdAsync(user.Id!, cancellationToken);

            await _emailSender.SendAsync(
                AuthEmailTemplates.PasswordReset(user.Email, user.Name, preferences?.Language ?? Language.PTBR, _frontendLinks.ResetPasswordUrl(token)),
                cancellationToken);

            return Result.Success();
        }

        /// <summary>The reset page checks its link first, so an expired one is shown before a password is typed.</summary>
        public async Task<Result<PasswordResetResponseDto>> VerifyAsync(EmailTokenDto emailTokenDto, CancellationToken cancellationToken)
        {
            var tokenResult = await GetUsableTokenAsync(emailTokenDto.Token, cancellationToken);
            if (!tokenResult.IsSuccess)
                return tokenResult.Error;

            var user = await _userRepository.GetByIdAsync(tokenResult.Value.UserId, cancellationToken);
            if (user is null)
                return AuthErrors.InvalidEmailToken;

            return new PasswordResetResponseDto(user.Email.Value);
        }

        /// <summary>
        /// Sets the new password and revokes every refresh token of the user (in the same database transaction), so other
        /// open sessions end on their next refresh. The link is checked before the password, and it works only once.
        /// </summary>
        public async Task<Result> ResetAsync(ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken)
        {
            var tokenResult = await GetUsableTokenAsync(resetPasswordDto.Token, cancellationToken);
            if (!tokenResult.IsSuccess)
                return tokenResult.Error;

            var plainPasswordResult = PlainPassword.Create(resetPasswordDto.Password);
            if (!plainPasswordResult.IsSuccess)
                return plainPasswordResult.Error;

            var passwordHashResult = PasswordHash.Create(_authService.EncryptPassword(plainPasswordResult.Value.Value));
            if (!passwordHashResult.IsSuccess)
                return passwordHashResult.Error;

            if (!await _passwordResetTokenRepository.UseAsync(tokenResult.Value, passwordHashResult.Value, _timeProvider.GetUtcNow(), cancellationToken))
                return AuthErrors.InvalidEmailToken;

            return Result.Success();
        }

        private async Task<Result<PasswordResetToken>> GetUsableTokenAsync(string token, CancellationToken cancellationToken)
        {
            var tokenHashResult = EmailTokenHash.FromToken(token);
            if (!tokenHashResult.IsSuccess)
                return tokenHashResult.Error;

            var passwordResetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHashResult.Value, cancellationToken);
            if (passwordResetToken is null || !passwordResetToken.IsUsable(_timeProvider.GetUtcNow()))
                return AuthErrors.InvalidEmailToken;

            return passwordResetToken;
        }
    }
}
