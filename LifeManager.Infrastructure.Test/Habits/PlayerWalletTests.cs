using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Habits
{
    /// <summary>
    /// Every game write of a user locks their profile first (PlayerWallet.LockAsync), so two writes at once apply one
    /// after the other: each one's decision reads the coins the previous one left.
    /// </summary>
    public class PlayerWalletTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private const int CoinsPerCheckIn = 10;
        private static readonly DateOnly Today = new(2026, 11, 10);

        [Fact]
        public async Task RecordAsync_ShouldApplyBothRewards_WhenTwoCheckInsOfTheSameUserRunConcurrently()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync();
            var reading = await seedContext.AddHabitAsync(user, "Ler", Today.AddDays(-3));
            var running = await seedContext.AddHabitAsync(user, "Correr", Today.AddDays(-3));
            // The profile already exists, so the first-change insert (ON CONFLICT on its unique index) can't be what
            // serializes the two writes: only the FOR UPDATE lock can.
            seedContext.Add(PlayerProfile.CreateDefault(user.Id!));
            await seedContext.SaveChangesAsync();

            var effects = await Task.WhenAll(new[] { reading, running }.Select(async habit =>
            {
                await using var context = Fixture.CreateContext();
                return await new HabitCheckInRepository(context).RecordAsync(habit, Today, HabitCheckInStatus.Done, TestData.Now, checkIn =>
                {
                    // Holds the lock long enough for the other check-in to be waiting on it, not running beside it.
                    Thread.Sleep(200);
                    var outcome = checkIn.Profile.Apply(new GameDelta(CoinsPerCheckIn, 0, 0));
                    var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.HabitDone, Today, TestData.Now, habit.Name.Value, outcome, habit.Id);

                    return new HabitCheckInEffects(1, outcome.Applied, outcome, entries);
                }, default);
            }));

            Assert.All(effects, Assert.NotNull);
            var profile = await seedContext.PlayerProfiles.AsNoTracking().SingleAsync();
            Assert.Equal(2 * CoinsPerCheckIn, profile.Coins);
            Assert.Equal(2, await seedContext.GameLedgerEntries.CountAsync(entry => entry.Kind == GameLedgerEntryKind.HabitDone));
            Assert.Equal(2, await seedContext.HabitCheckIns.CountAsync());
        }
    }
}
