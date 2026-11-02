using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.RankingGroups.Mocks;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Ranking.Mocks
{
    /// <summary>Mirrors RankingWeekResultRepository over the singletons: the unique week index and the group's cascade.</summary>
    public class RankingWeekResultRepositoryMock : IRankingWeekResultRepository
    {
        private static readonly object Gate = new();

        static RankingWeekResultRepositoryMock()
        {
            RankingGroupRepositoryMock.GroupDeleted += groupId =>
                RankingWeekResultSingleton.Instance.RemoveAll(result => result.GroupId == groupId);
        }

        /// <summary>Runs right before the next <see cref="AddWeekAsync"/> stores anything, to simulate a concurrent close.</summary>
        public static Action? BeforeNextAdd { get; set; }

        public static int AddCalls { get; private set; }

        public static void Reset()
        {
            BeforeNextAdd = null;
            AddCalls = 0;
        }

        public Task<IReadOnlyList<RankingWeekResultView>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            IReadOnlyList<RankingWeekResultView> views = [.. RankingWeekResultSingleton.Instance
                .Where(result => result.GroupId == groupId && result.WeekStart == weekStart)
                .OrderBy(result => result.Position)
                .ThenBy(result => result.Id!.Value)
                .Select(result =>
                {
                    var member = RankingGroupMemberSingleton.Instance.SingleOrDefault(candidate => candidate.GroupId == groupId && candidate.UserId == result.UserId);
                    var profile = PlayerProfileSingleton.Instance.SingleOrDefault(candidate => candidate.UserId == result.UserId);
                    var name = UserSingleton.Instance.Single(user => user.Id == result.UserId).Name.Value;

                    return new RankingWeekResultView(result, member?.Id, name, profile?.Avatar ?? PlayerAvatar.Cat, profile?.Title);
                })];

            return Task.FromResult(views);
        }

        public Task<DateOnly?> GetLatestClosedWeekAsync(RankingGroupId groupId, CancellationToken cancellationToken)
            => Task.FromResult(RankingWeekResultSingleton.Instance
                .Where(result => result.GroupId == groupId)
                .Select(result => (DateOnly?)result.WeekStart)
                .Max());

        public Task<IReadOnlyList<RankingGroupId>> GetGroupsToCloseAsync(DateOnly lastSettledWeekStart, CancellationToken cancellationToken)
        {
            var lastSettledSunday = GameWeek.End(lastSettledWeekStart);

            IReadOnlyList<RankingGroupId> ids = [.. RankingGroupSingleton.Instance
                .Where(group => group.CreatedOn <= lastSettledSunday
                    && !RankingWeekResultSingleton.Instance.Any(result => result.GroupId == group.Id && result.WeekStart == lastSettledWeekStart))
                .OrderBy(group => group.Id!.Value)
                .Select(group => group.Id!)];

            return Task.FromResult(ids);
        }

        public Task<bool> AreDaysClosedAsync(IReadOnlyCollection<UserId> userIds, DateOnly day, CancellationToken cancellationToken)
            => Task.FromResult(!HabitSingleton.Instance.Any(habit => userIds.Contains(habit.UserId) && habit.ArchivedAt is null && habit.EvaluatedUntil < day));

        public Task<bool> AddWeekAsync(IReadOnlyList<RankingWeekResult> results, CancellationToken cancellationToken)
        {
            AddCalls++;
            var beforeNextAdd = BeforeNextAdd;
            BeforeNextAdd = null;
            beforeNextAdd?.Invoke();

            lock (Gate)
            {
                // The unique (GroupId, WeekStart, UserId) index refuses the whole batch.
                if (results.Any(result => RankingWeekResultSingleton.Instance.Any(stored =>
                    stored.GroupId == result.GroupId && stored.WeekStart == result.WeekStart && stored.UserId == result.UserId)))
                    return Task.FromResult(false);

                foreach (var result in results)
                {
                    result.AssignId(RankingWeekResultSingleton.Instance.Select(stored => stored.Id!.Value).DefaultIfEmpty(0).Max() + 1);
                    RankingWeekResultSingleton.Instance.Add(result);
                }

                return Task.FromResult(true);
            }
        }
    }
}
