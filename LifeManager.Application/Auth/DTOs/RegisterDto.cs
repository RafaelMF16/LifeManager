using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Application.Auth.DTOs
{
    /// <param name="Language">The sign-up screen's language: the e-mail's, the preferences' and the default categories'.</param>
    /// <param name="Theme">The sign-up screen's theme, saved as the user's preference.</param>
    public record RegisterDto(
        string Email,
        string Name,
        Language? Language = null,
        Theme? Theme = null);
}
