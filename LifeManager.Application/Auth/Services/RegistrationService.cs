using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Emails;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
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
        AuthService authService,
        TimeProvider timeProvider)
    {
        private readonly AuthService _authService = authService;
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
                AuthEmailTemplates.Confirmation(email, pendingRegistration.Language, _frontendLinks.SetPasswordUrl(token)),
                cancellationToken);

            return Result.Success();
        }

        /// <summary>The set-password page checks its link first, so an expired one is shown before a password is typed.</summary>
        public async Task<Result<PendingRegistrationResponseDto>> VerifyAsync(EmailTokenDto emailTokenDto, CancellationToken cancellationToken)
        {
            var pendingResult = await GetUsablePendingRegistrationAsync(emailTokenDto.Token, cancellationToken);
            if (!pendingResult.IsSuccess)
                return pendingResult.Error;

            return new PendingRegistrationResponseDto(pendingResult.Value.Email.Value, pendingResult.Value.Name.Value);
        }

        /// <summary>
        /// Creates the account with the chosen password, the sign-up's preferences and default categories. The link is
        /// checked before the password, so an expired link is reported as such whatever was typed.
        /// </summary>
        public async Task<Result> CompleteAsync(CompleteRegistrationDto completeRegistrationDto, CancellationToken cancellationToken)
        {
            var pendingResult = await GetUsablePendingRegistrationAsync(completeRegistrationDto.Token, cancellationToken);
            if (!pendingResult.IsSuccess)
                return pendingResult.Error;

            var pendingRegistration = pendingResult.Value;

            var plainPasswordResult = PlainPassword.Create(completeRegistrationDto.Password);
            if (!plainPasswordResult.IsSuccess)
                return plainPasswordResult.Error;

            var hashedPassword = _authService.EncryptPassword(plainPasswordResult.Value.Value);
            var userResult = User.Create(pendingRegistration.Name.Value, pendingRegistration.Email.Value, hashedPassword);
            if (!userResult.IsSuccess)
                return userResult.Error;

            var user = await _pendingRegistrationRepository.CompleteAsync(pendingRegistration, userResult.Value, cancellationToken);
            if (user is null)
                return AuthErrors.InvalidEmailToken;

            return Result.Success();
        }

        private async Task<Result<PendingRegistration>> GetUsablePendingRegistrationAsync(string token, CancellationToken cancellationToken)
        {
            var tokenHashResult = EmailTokenHash.FromToken(token);
            if (!tokenHashResult.IsSuccess)
                return tokenHashResult.Error;

            var pendingRegistration = await _pendingRegistrationRepository.GetByTokenHashAsync(tokenHashResult.Value, cancellationToken);
            if (pendingRegistration is null || pendingRegistration.IsExpired(_timeProvider.GetUtcNow()))
                return AuthErrors.InvalidEmailToken;

            return pendingRegistration;
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
