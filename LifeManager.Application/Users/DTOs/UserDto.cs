using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Application.Users.DTOs
{
    /// <param name="Language">The sign-up screen's language: the default categories' and the preferences' language.</param>
    /// <param name="Theme">The sign-up screen's theme, saved as the user's preference.</param>
    public record UserDto(
        string Email,
        string Name,
        string UserPassword,
        Language? Language = null,
        Theme? Theme = null);
}
