using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Users
{
    /// <summary>The account deletion's final step against the real schema: groups left by the rule, then the user's cascade.</summary>
    public class AccountDeletionRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly Today = new(2026, 11, 10);
        private static readonly DateTimeOffset Due = TestData.Now.AddDays(-AccountDeletion.GraceDays);

        [Fact]
        public async Task GetDueUserIdsAsync_ShouldListOnlyDueDeletionsOldestFirst()
        {
            await using var context = Fixture.CreateContext();
            var ana = await AddScheduledUserAsync(context, "Ana", TestData.Now.AddDays(-1));
            var bia = await AddScheduledUserAsync(context, "Bia", TestData.Now.AddDays(-3));
            await AddScheduledUserAsync(context, "Caio", TestData.Now.AddDays(1));
            await context.AddUserAsync("Duda");

            var due = await new AccountDeletionRepository(context).GetDueUserIdsAsync(TestData.Now, 10, CancellationToken.None);

            Assert.Equal([bia.Id!, ana.Id!], due);
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldLeaveEveryGroupByTheRuleAndDeleteTheUser()
        {
            await using var seedContext = Fixture.CreateContext();
            var ana = await AddScheduledUserAsync(seedContext, "Ana", Due);
            var bia = await seedContext.AddUserAsync("Bia");
            var caio = await seedContext.AddUserAsync("Caio");
            // Ana owns "Família" with Bia (joined first) and Caio, is alone in "Solo" and is a member of Bia's "Trabalho".
            var family = await AddGroupAsync(seedContext, "Família", ana, (bia, 1), (caio, 2));
            var solo = await AddGroupAsync(seedContext, "Solo", ana);
            var work = await AddGroupAsync(seedContext, "Trabalho", bia, (ana, 1));

            await using (var context = Fixture.CreateContext())
                Assert.True(await new AccountDeletionRepository(context).DeleteIfDueAsync(ana.Id!, TestData.Now, CancellationToken.None));

            await using var readContext = Fixture.CreateContext();
            Assert.False(await readContext.Users.AnyAsync(user => user.Id == ana.Id));
            var groups = await readContext.RankingGroups.AsNoTracking().OrderBy(group => group.Id).ToListAsync();
            Assert.Equal([family.Id!, work.Id!], groups.Select(group => group.Id!));
            Assert.Equal(bia.Id, groups[0].OwnerUserId);
            Assert.Equal(bia.Id, groups[1].OwnerUserId);
            Assert.DoesNotContain(groups, group => group.Id == solo.Id);
            Assert.Equal(
                [(family.Id!.Value, bia.Id!.Value), (family.Id!.Value, caio.Id!.Value), (work.Id!.Value, bia.Id!.Value)],
                (await readContext.RankingGroupMembers.AsNoTracking().ToListAsync())
                    .Select(member => (member.GroupId.Value, member.UserId.Value))
                    .Order());
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldWriteNothing_WhenTheDeletionWasCancelled()
        {
            await using var seedContext = Fixture.CreateContext();
            var ana = await AddScheduledUserAsync(seedContext, "Ana", Due);
            var bia = await seedContext.AddUserAsync("Bia");
            var family = await AddGroupAsync(seedContext, "Família", ana, (bia, 1));

            await using (var context = Fixture.CreateContext())
                await new UserRepository(context).CancelDeletionAsync(ana.Id!, CancellationToken.None);

            await using (var context = Fixture.CreateContext())
                Assert.False(await new AccountDeletionRepository(context).DeleteIfDueAsync(ana.Id!, TestData.Now, CancellationToken.None));

            await using var readContext = Fixture.CreateContext();
            Assert.True(await readContext.Users.AnyAsync(user => user.Id == ana.Id));
            Assert.Equal(ana.Id, (await readContext.RankingGroups.AsNoTracking().SingleAsync(group => group.Id == family.Id)).OwnerUserId);
            Assert.Equal(2, await readContext.RankingGroupMembers.CountAsync());
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldWriteNothing_BeforeTheDate()
        {
            await using var seedContext = Fixture.CreateContext();
            var ana = await AddScheduledUserAsync(seedContext, "Ana", TestData.Now.AddHours(1));

            await using (var context = Fixture.CreateContext())
                Assert.False(await new AccountDeletionRepository(context).DeleteIfDueAsync(ana.Id!, TestData.Now, CancellationToken.None));

            await using var readContext = Fixture.CreateContext();
            Assert.True(await readContext.Users.AnyAsync(user => user.Id == ana.Id));
        }

        /// <summary>A user whose deletion is due at <paramref name="scheduledFor"/> (requested a grace period before).</summary>
        private static async Task<User> AddScheduledUserAsync(LifeManagerDbContext context, string name, DateTimeOffset scheduledFor)
        {
            var user = await context.AddUserAsync(name);
            await new UserRepository(context).ScheduleDeletionAsync(user.Id!, scheduledFor.AddDays(-AccountDeletion.GraceDays), scheduledFor, CancellationToken.None);
            return user;
        }

        private static async Task<RankingGroup> AddGroupAsync(LifeManagerDbContext context, string name, User owner, params (User User, int DaysAfter)[] members)
        {
            var group = RankingGroup.Create(name, owner.Id!, InviteCode.Generate(), TestData.Now, Today).Unwrap();
            context.Add(group);
            await context.SaveChangesAsync();

            context.Add(RankingGroupMember.Create(group.Id!, owner.Id!, TestData.Now, Today));
            foreach (var (member, daysAfter) in members)
                context.Add(RankingGroupMember.Create(group.Id!, member.Id!, TestData.Now.AddDays(daysAfter), Today.AddDays(daysAfter)));
            await context.SaveChangesAsync();

            return group;
        }
    }
}
