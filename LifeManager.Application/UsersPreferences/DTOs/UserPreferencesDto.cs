using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Application.UsersPreferences.DTOs
{
    public record UserPreferencesDto(Theme Theme, Language Language);
}