using LifeManager.Domain.Habits.Challenges;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class WeeklyChallengeSingleton : List<WeeklyChallenge>
    {
        private WeeklyChallengeSingleton() { }

        private static readonly Lazy<WeeklyChallengeSingleton> lazy = new(() => new WeeklyChallengeSingleton());

        public static WeeklyChallengeSingleton Instance => lazy.Value;
    }
}
