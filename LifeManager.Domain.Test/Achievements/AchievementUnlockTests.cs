using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Achievements
{
    public class AchievementUnlockTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 28, 13, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_ShouldStartUnseen()
        {
            var unlock = AchievementUnlock.Create(new UserId(1), AchievementCode.CheckIns10, new DateOnly(2026, 10, 28), Now);

            Assert.Equal(AchievementCode.CheckIns10, unlock.Code);
            Assert.Equal(new DateOnly(2026, 10, 28), unlock.UnlockedOn);
            Assert.Equal(Now, unlock.UnlockedAt);
            Assert.False(unlock.IsSeen);
        }

        [Fact]
        public void MarkSeen_ShouldKeepTheFirstTime()
        {
            var unlock = AchievementUnlock.Create(new UserId(1), AchievementCode.CheckIns10, new DateOnly(2026, 10, 28), Now);

            unlock.MarkSeen(Now.AddMinutes(1));
            unlock.MarkSeen(Now.AddMinutes(5));

            Assert.True(unlock.IsSeen);
            Assert.Equal(Now.AddMinutes(1), unlock.SeenAt);
        }
    }
}
