using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;

namespace LifeManager.Domain.Habits.Challenges
{
    public record ChallengeSuggestion(HabitId HabitId, ChallengeType Type, int Target, decimal Baseline, ChallengeEffort Effort, ChallengeReward Reward);
}
