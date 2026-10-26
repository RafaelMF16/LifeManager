using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Emails;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// The two-step sign-up: name and e-mail first (<see cref="RegisterAsync"/>), then the password, through the link
    /// sent by e-mail. The answer never tells whether the e-mail already has an account; the e-mail does.
    /// </summary>
    public class RegistrationService(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IPendingRegistrationRepository pendingRegistrationRepository,
        IEmailCooldownRepository emailCooldownRepository,
        IEmailSender emailSender,
        FrontendLinks frontendLinks,
        TimeProvider timeProvider)
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUserPreferencesRepository _userPreferencesRepository = userPreferencesRepository;
        private readonly IPendingRegistrationRepository _pendingRegistrationRepository = pendingRegistrationRepository;
        private readonly IEmailCooldownRepository _emailCooldownRepository = emailCooldownRepository;
        private readonly IEmailSender _emailSender = emailSender;
        private readonly FrontendLinks _frontendLinks = frontendLinks;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// A new e-mail gets (or renews) its pending sign-up and the confirmation link; an e-mail with an account gets
        /// the "you already have an account" e-mail instead. Inside the cooldown nothing changes, so the link already
        /// sent stays valid. A failed send is already logged by the sender and still succeeds.
        /// </summary>
        public async Task<Result> RegisterAsync(RegisterDto registerDto, CancellationToken cancellationToken)
        {
            var emailResult = Email.Create(registerDto.Email);
            if (!emailResult.IsSuccess)
                return emailResult.Error;

            var nameResult = UserName.Create(registerDto.Name);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            var email = emailResult.Value;
            var now = _timeProvider.GetUtcNow();

            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (user is not null)
            {
                if (await _emailCooldownRepository.TryReserveAsync(email, EmailKind.AccountExists, now, cancellationToken))
                    await SendAccountExistsAsync(user, cancellationToken);

                return Result.Success();
            }

            if (!await _emailCooldownRepository.TryReserveAsync(email, EmailKind.Confirmation, now, cancellationToken))
                return Result.Success();

            var (token, tokenHash) = EmailTokenGenerator.Generate();

            var pendingRegistration = await _pendingRegistrationRepository.GetByEmailAsync(email, cancellationToken);
            if (pendingRegistration is null)
            {
                var createResult = PendingRegistration.Create(registerDto.Name, registerDto.Email, registerDto.Language, registerDto.Theme, tokenHash, now);
                if (!createResult.IsSuccess)
                    return createResult.Error;

                pendingRegistration = createResult.Value;
            }
            else
            {
                var renewResult = pendingRegistration.Renew(registerDto.Name, registerDto.Language, registerDto.Theme, tokenHash, now);
                if (!renewResult.IsSuccess)
                    return renewResult.Error;
            }

            await _pendingRegistrationRepository.SaveAsync(pendingRegistration, cancellationToken);

            await _emailSender.SendAsync(
                AuthEmailTemplates.Confirmation(email, pendingRegistration.Name, pendingRegistration.Language, _frontendLinks.SetPasswordUrl(token)),
                cancellationToken);

            return Result.Success();
        }

        private async Task SendAccountExistsAsync(User user, CancellationToken cancellationToken)
        {
            var preferences = await _userPreferencesRepository.GetByUserIdAsync(user.Id!, cancellationToken);

            await _emailSender.SendAsync(
                AuthEmailTemplates.AccountExists(user.Email, user.Name, preferences?.Language ?? Language.PTBR, _frontendLinks.ForgotPasswordUrl()),
                cancellationToken);
        }
    }
}
