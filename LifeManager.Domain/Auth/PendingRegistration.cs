using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Domain.Auth
{
    /// <summary>
    /// A sign-up waiting for its e-mail to be confirmed: the name, e-mail and the sign-up screen's language and theme,
    /// plus the hash of the token in the confirmation link. The account only exists once the link is opened and a
    /// password set; then this row is deleted. One per e-mail: signing up again renews it and the old link stops working.
    /// </summary>
    public class PendingRegistration
    {
        public const int ValidityHours = 24;

        /// <summary>Days an expired sign-up is kept before the hourly cleanup deletes it.</summary>
        public const int RetentionDays = 7;

        public PendingRegistrationId? Id { get; private set; }
        public UserName Name { get; private set; }
        public Email Email { get; private set; }
        public Language Language { get; private set; }
        public Theme Theme { get; private set; }
        public EmailTokenHash TokenHash { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }

        private PendingRegistration(
            UserName name,
            Email email,
            Language language,
            Theme theme,
            EmailTokenHash tokenHash,
            DateTimeOffset createdAt,
            DateTimeOffset expiresAt)
        {
            Name = name;
            Email = email;
            Language = language;
            Theme = theme;
            TokenHash = tokenHash;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public static Result<PendingRegistration> Create(
            string name,
            string email,
            Language? language,
            Theme? theme,
            EmailTokenHash tokenHash,
            DateTimeOffset now)
        {
            return UserName.Create(name)
                .Bind(userName => Email.Create(email)
                    .Map(userEmail => new PendingRegistration(
                        userName,
                        userEmail,
                        ResolveLanguage(language),
                        ResolveTheme(theme),
                        tokenHash,
                        now,
                        now.AddHours(ValidityHours))));
        }

        public Result Renew(string name, Language? language, Theme? theme, EmailTokenHash tokenHash, DateTimeOffset now)
        {
            var nameResult = UserName.Create(name);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            Name = nameResult.Value;
            Language = ResolveLanguage(language);
            Theme = ResolveTheme(theme);
            TokenHash = tokenHash;
            ExpiresAt = now.AddHours(ValidityHours);

            return Result.Success();
        }

        public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

        public void AssignId(int id)
        {
            Id = new PendingRegistrationId(id);
        }

        // Same fallbacks as UserPreferences.CreateDefault, which receives these values when the account is created.
        private static Language ResolveLanguage(Language? language)
            => language is { } given && Enum.IsDefined(given) ? given : Language.PTBR;

        private static Theme ResolveTheme(Theme? theme)
            => theme is { } given && Enum.IsDefined(given) ? given : Theme.Light;
    }
}
