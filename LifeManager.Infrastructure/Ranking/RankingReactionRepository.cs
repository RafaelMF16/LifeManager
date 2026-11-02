using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LifeManager.Infrastructure.Ranking
{
    public class RankingReactionRepository(LifeManagerDbContext dbContext) : IRankingReactionRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<IReadOnlyList<RankingReaction>> GetWeekAsync(RankingGroupId groupId, DateOnly weekStart, CancellationToken cancellationToken)
            => await _dbContext.RankingReactions
                .AsNoTracking()
                .Where(reaction => reaction.GroupId == groupId && reaction.WeekStart == weekStart)
                .OrderBy(reaction => reaction.CreatedAt)
                .ThenBy(reaction => reaction.Id)
                .ToListAsync(cancellationToken);

        public async Task<bool> SetAsync(RankingReaction reaction, CancellationToken cancellationToken)
        {
            var groupId = reaction.GroupId.Value;
            var fromMemberId = reaction.FromMemberId.Value;
            var toMemberId = reaction.ToMemberId.Value;
            var kind = (int)reaction.Kind;

            try
            {
                // One statement, so two taps at once can't both insert: the second one changes the kind.
                await _dbContext.Database.ExecuteSqlAsync($"""
                    INSERT INTO "RankingReactions" ("GroupId", "WeekStart", "FromMemberId", "ToMemberId", "Kind", "CreatedAt")
                    VALUES ({groupId}, {reaction.WeekStart}, {fromMemberId}, {toMemberId}, {kind}, {reaction.CreatedAt})
                    ON CONFLICT ("GroupId", "WeekStart", "FromMemberId", "ToMemberId")
                    DO UPDATE SET "Kind" = EXCLUDED."Kind", "CreatedAt" = EXCLUDED."CreatedAt"
                    """, cancellationToken);

                return true;
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return false;
            }
        }

        public async Task RemoveAsync(
            RankingGroupId groupId, DateOnly weekStart, RankingGroupMemberId fromMemberId, RankingGroupMemberId toMemberId, CancellationToken cancellationToken)
        {
            await _dbContext.RankingReactions
                .Where(reaction => reaction.GroupId == groupId
                    && reaction.WeekStart == weekStart
                    && reaction.FromMemberId == fromMemberId
                    && reaction.ToMemberId == toMemberId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
