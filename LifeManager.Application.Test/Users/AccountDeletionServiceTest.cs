using LifeManager.Application.RankingGroups.DTOs;
using LifeManager.Application.RankingGroups.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RankingGroups;
using LifeManager.Application.Test.RankingGroups.Mocks;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Users
{
    [Collection("ApplicationServices")]
    public class AccountDeletionServiceTest : BaseTest
    {
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        private static readonly DateOnly Today = new(2026, 11, 4);

        private readonly AccountDeletionService _service;
        private readonly RankingGroupService _groups;
        private readonly FakeTimeProvider _clock;

        public AccountDeletionServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<AccountDeletionService>();
            _groups = ServiceProvider.GetRequiredService<RankingGroupService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _clock.SetToday(Today);

            RankingGroupSingleton.Instance.Clear();
            RankingGroupMemberSingleton.Instance.Clear();
            RankingReactionSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            RefreshTokenSingleton.Instance.Clear();
            RankingGroupRepositoryMock.BeforeNextWrite = null;
            AccountDeletionRepositoryMock.BeforeNextDelete = null;
            RankingGroupServiceTest.SeedUsers(5);
        }

        private static User UserOf(UserId id) => UserSingleton.Instance.Single(user => user.Id == id);

        private void ScheduleDeletion(UserId id, int daysAgo)
            => UserOf(id).ScheduleDeletion(_clock.UtcNow.AddDays(-daysAgo));

        private async Task<RankingGroupDetailsDto> CreateGroupAsync(UserId owner)
            => (await _groups.CreateAsync(new RankingGroupRequestDto("Família"), owner, CancellationToken.None)).Value!;

        private async Task JoinAsync(RankingGroupDetailsDto group, UserId userId)
            => Assert.True((await _groups.JoinAsync(group.InviteCode, userId, CancellationToken.None)).IsSuccess);

        [Fact]
        public async Task GetDueUserIdsAsync_ShouldListOnlyAccountsPastTheirGracePeriod()
        {
            ScheduleDeletion(Ana, AccountDeletion.GraceDays);
            ScheduleDeletion(Bia, AccountDeletion.GraceDays - 1);

            var due = await _service.GetDueUserIdsAsync(CancellationToken.None);

            Assert.Equal([Ana], due);
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldDeleteTheUser_WhenTheGracePeriodIsOver()
        {
            ScheduleDeletion(Ana, AccountDeletion.GraceDays);

            Assert.True(await _service.DeleteIfDueAsync(Ana, CancellationToken.None));

            Assert.DoesNotContain(UserSingleton.Instance, user => user.Id == Ana);
            Assert.Contains(UserSingleton.Instance, user => user.Id == Bia);
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldKeepTheUser_BeforeTheEndOfTheGracePeriod()
        {
            ScheduleDeletion(Ana, AccountDeletion.GraceDays - 1);

            Assert.False(await _service.DeleteIfDueAsync(Ana, CancellationToken.None));

            Assert.Contains(UserSingleton.Instance, user => user.Id == Ana);
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldKeepTheUser_WhenTheySignedInBetweenTheLookupAndTheLock()
        {
            ScheduleDeletion(Ana, AccountDeletion.GraceDays);
            var due = await _service.GetDueUserIdsAsync(CancellationToken.None);
            AccountDeletionRepositoryMock.BeforeNextDelete = () => UserOf(Ana).CancelDeletion();

            Assert.False(await _service.DeleteIfDueAsync(Assert.Single(due), CancellationToken.None));

            Assert.Contains(UserSingleton.Instance, user => user.Id == Ana);
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldPassAnOwnedGroupToTheOldestMember()
        {
            var group = await CreateGroupAsync(Ana);
            _clock.SetToday(Today.AddDays(1));
            await JoinAsync(group, Bia);
            _clock.SetToday(Today.AddDays(2));
            await JoinAsync(group, Caio);
            ScheduleDeletion(Ana, AccountDeletion.GraceDays);

            Assert.True(await _service.DeleteIfDueAsync(Ana, CancellationToken.None));

            var stored = Assert.Single(RankingGroupSingleton.Instance);
            Assert.Equal(Bia, stored.OwnerUserId);
            Assert.Equal([Bia, Caio], RankingGroupMemberSingleton.Instance.Select(member => member.UserId).OrderBy(id => id.Value));
        }

        [Fact]
        public async Task DeleteIfDueAsync_ShouldDeleteAGroupWhereTheUserWasAlone()
        {
            await CreateGroupAsync(Ana);
            var biaGroup = await CreateGroupAsync(Bia);
            await JoinAsync(biaGroup, Ana);
            ScheduleDeletion(Ana, AccountDeletion.GraceDays);

            Assert.True(await _service.DeleteIfDueAsync(Ana, CancellationToken.None));

            var remaining = Assert.Single(RankingGroupSingleton.Instance);
            Assert.Equal(biaGroup.Id, remaining.Id!.Value);
            Assert.Equal(Bia, remaining.OwnerUserId);
            Assert.Equal([Bia], RankingGroupMemberSingleton.Instance.Select(member => member.UserId));
        }
    }
}
