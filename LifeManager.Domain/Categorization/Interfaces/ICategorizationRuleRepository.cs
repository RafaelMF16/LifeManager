using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categorization.Interfaces
{
    /// <remarks>Rules are written only by an import's confirmation (<c>IStatementImportRepository.CommitAsync</c>).</remarks>
    public interface ICategorizationRuleRepository
    {
        /// <summary>
        /// Every rule of the user: one per merchant and decision, so bounded by how many merchants they have imported
        /// (the suggester compares keys in memory).
        /// </summary>
        Task<IReadOnlyList<CategorizationRule>> GetByUserIdAsync(UserId userId, CancellationToken cancellationToken);
    }
}
