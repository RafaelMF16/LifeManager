using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;

namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// Deletes what the e-mail auth flows leave behind once it can no longer decide anything: sign-ups that expired
    /// <see cref="PendingRegistration.RetentionDays"/> days ago, reset links expired or used
    /// <see cref="PasswordResetToken.RetentionDays"/> days ago, and cooldowns older than <see cref="EmailCooldown.Retention"/>.
    /// </summary>
    public class AuthCleanupService(
        IPendingRegistrationRepository pendingRegistrationRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IEmailCooldownRepository emailCooldownRepository,
        TimeProvider timeProvider)
    {
        private readonly IPendingRegistrationRepository _pendingRegistrationRepository = pendingRegistrationRepository;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = passwordResetTokenRepository;
        private readonly IEmailCooldownRepository _emailCooldownRepository = emailCooldownRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<AuthCleanupResult> DeleteStaleAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            var pendingRegistrations = await _pendingRegistrationRepository.DeleteExpiredBeforeAsync(
                now.AddDays(-PendingRegistration.RetentionDays), cancellationToken);
            var passwordResetTokens = await _passwordResetTokenRepository.DeleteStaleBeforeAsync(
                now.AddDays(-PasswordResetToken.RetentionDays), cancellationToken);
            var emailCooldowns = await _emailCooldownRepository.DeleteOlderThanAsync(now - EmailCooldown.Retention, cancellationToken);

            return new AuthCleanupResult(pendingRegistrations, passwordResetTokens, emailCooldowns);
        }
    }

    public record AuthCleanupResult(int PendingRegistrations, int PasswordResetTokens, int EmailCooldowns)
    {
        public int Total => PendingRegistrations + PasswordResetTokens + EmailCooldowns;
    }
}
