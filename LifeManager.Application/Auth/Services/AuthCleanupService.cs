using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;

namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// Deletes what the e-mail auth flows leave behind once it can no longer decide anything: sign-ups that expired
    /// <see cref="PendingRegistration.RetentionDays"/> days ago, reset links expired or used
    /// <see cref="PasswordResetToken.RetentionDays"/> days ago, cooldowns older than <see cref="EmailCooldown.Retention"/>,
    /// and login attempts whose window ended more than <see cref="LoginAttempt.Retention"/> ago.
    /// </summary>
    public class AuthCleanupService(
        IPendingRegistrationRepository pendingRegistrationRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IEmailCooldownRepository emailCooldownRepository,
        ILoginAttemptRepository loginAttemptRepository,
        TimeProvider timeProvider)
    {
        private readonly IPendingRegistrationRepository _pendingRegistrationRepository = pendingRegistrationRepository;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = passwordResetTokenRepository;
        private readonly IEmailCooldownRepository _emailCooldownRepository = emailCooldownRepository;
        private readonly ILoginAttemptRepository _loginAttemptRepository = loginAttemptRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<AuthCleanupResult> DeleteStaleAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            var pendingRegistrations = await _pendingRegistrationRepository.DeleteExpiredBeforeAsync(
                now.AddDays(-PendingRegistration.RetentionDays), cancellationToken);
            var passwordResetTokens = await _passwordResetTokenRepository.DeleteStaleBeforeAsync(
                now.AddDays(-PasswordResetToken.RetentionDays), cancellationToken);
            var emailCooldowns = await _emailCooldownRepository.DeleteOlderThanAsync(now - EmailCooldown.Retention, cancellationToken);

            var loginAttempts = await _loginAttemptRepository.DeleteWindowsStartedBeforeAsync(
                now - LoginAttempt.Window - LoginAttempt.Retention, cancellationToken);

            return new AuthCleanupResult(pendingRegistrations, passwordResetTokens, emailCooldowns, loginAttempts);
        }
    }

    public record AuthCleanupResult(int PendingRegistrations, int PasswordResetTokens, int EmailCooldowns, int LoginAttempts)
    {
        public int Total => PendingRegistrations + PasswordResetTokens + EmailCooldowns + LoginAttempts;
    }
}
