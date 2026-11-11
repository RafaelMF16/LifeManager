using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RankingGroups.Mocks;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Users.Mocks
{
    /// <summary>
    /// Mirrors <c>AccountDeletionRepository</c>: the due check again right before deleting, leaving every group through the
    /// ranking groups mock's own leave (the same rule), then removing the user. The other tables' cascades are proved by
    /// the integration tests, not here.
    /// </summary>
    public class AccountDeletionRepositoryMock : IAccountDeletionRepository
    {
        /// <summary>Runs right before the next due check under the lock, e.g. a sign-in that cancels the deletion.</summary>
        public static Action? BeforeNextDelete { get; set; }

        public Task<IReadOnlyList<UserId>> GetDueUserIdsAsync(DateTimeOffset now, int maxCount, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UserId>>(UserSingleton.Instance
                .Where(user => user.DeletionScheduledFor is { } scheduledFor && scheduledFor <= now)
                .OrderBy(user => user.DeletionScheduledFor)
                .ThenBy(user => user.Id!.Value)
                .Select(user => user.Id!)
                .Take(maxCount)
                .ToList());

        public async Task<bool> DeleteIfDueAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var beforeNextDelete = BeforeNextDelete;
            BeforeNextDelete = null;
            beforeNextDelete?.Invoke();

            var user = UserSingleton.Instance.SingleOrDefault(stored => stored.Id == userId);
            if (user?.DeletionScheduledFor is not { } scheduledFor || scheduledFor > now)
                return false;

            var groups = new RankingGroupRepositoryMock();
            var groupIds = RankingGroupMemberSingleton.Instance
                .Where(member => member.UserId == userId)
                .Select(member => member.GroupId)
                .OrderBy(groupId => groupId.Value)
                .ToList();
            foreach (var groupId in groupIds)
                await groups.LeaveAsync(groupId, userId, cancellationToken);

            UserSingleton.Instance.Remove(user);
            RefreshTokenSingleton.Instance.RemoveAll(token => token.UserId.Equals(userId));

            return true;
        }
    }
}
