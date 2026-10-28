using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Body of <c>PUT /api/Habits/Profile/Appearance</c>.</summary>
    /// <param name="Title">An unlocked achievement whose title to show; null for none.</param>
    public record UpdateAppearanceRequestDto(PlayerAvatar Avatar, AchievementCode? Title);
}
