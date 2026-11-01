using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Ranking.Interfaces
{
    public interface IWeeklyScoreRepository
    {
        /// <summary>
        /// The habits that may count in the week (started by <paramref name="weekEnd"/>, archived ones included) and the
        /// check-ins from <paramref name="weekStart"/> to <paramref name="weekEnd"/> of <paramref name="userIds"/>.
        /// </summary>
        Task<RankingScoreInputs> GetInputsAsync(IReadOnlyCollection<UserId> userIds, DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken);
    }
}
