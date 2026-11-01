using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LifeManager.Infrastructure.Ranking
{
    public class RankingWeekResultRepository(LifeManagerDbContext dbContext) : IRankingWeekResultRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<RankingWeekResultView>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            var rows = await _dbContext.RankingWeekResults
                .AsNoTracking()
                .Where(result => result.GroupId == groupId && result.WeekStart == weekStart)
                .OrderBy(result => result.Position)
                .ThenBy(result => result.Id)
                .Select(result => new
                {
                    Result = result,
                    MemberId = _dbContext.RankingGroupMembers
                        .Where(member => member.GroupId == result.GroupId && member.UserId == result.UserId)
                        .Select(member => member.Id)
                        .FirstOrDefault(),
                    Name = _dbContext.Users.Where(user => user.Id == result.UserId).Select(user => user.Name).First(),
                    Profile = _dbContext.PlayerProfiles
                        .Where(profile => profile.UserId == result.UserId)
                        .Select(profile => new { profile.Avatar, profile.Title })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => new RankingWeekResultView(
                row.Result, row.MemberId, row.Name.Value, row.Profile?.Avatar ?? PlayerAvatar.Cat, row.Profile?.Title))];
        }

        public async Task<DateOnly?> GetLatestClosedWeekAsync(RankingGroupId groupId, CancellationToken cancellationToken)
        {
            return await _dbContext.RankingWeekResults
                .Where(result => result.GroupId == groupId)
                .MaxAsync(result => (DateOnly?)result.WeekStart, cancellationToken);
        }

        public async Task<IReadOnlyList<RankingGroupId>> GetGroupsToCloseAsync(DateOnly lastSettledWeekStart, CancellationToken cancellationToken)
        {
            var lastSettledSunday = GameWeek.End(lastSettledWeekStart);

            var ids = await _dbContext.RankingGroups
                .AsNoTracking()
                .Where(group => group.CreatedOn <= lastSettledSunday
                    && !_dbContext.RankingWeekResults.Any(result => result.GroupId == group.Id && result.WeekStart == lastSettledWeekStart))
                .OrderBy(group => group.Id)
                .Select(group => group.Id)
                .ToListAsync(cancellationToken);

            return [.. ids.Select(id => id!)];
        }

        public async Task<bool> AreDaysClosedAsync(IReadOnlyCollection<UserId> userIds, DateOnly day, CancellationToken cancellationToken)
        {
            var ids = userIds.Distinct().ToList();

            return !await _dbContext.Habits.AnyAsync(
                habit => ids.Contains(habit.UserId) && habit.ArchivedAt == null && habit.EvaluatedUntil < day,
                cancellationToken);
        }

        public async Task<bool> AddWeekAsync(IReadOnlyList<RankingWeekResult> results, CancellationToken cancellationToken)
        {
            _dbContext.RankingWeekResults.AddRange(results);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                _dbContext.ChangeTracker.Clear();
                return false;
            }
        }
    }
}
