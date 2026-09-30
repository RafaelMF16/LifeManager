using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category> AddAsync(Category category, CancellationToken cancellationToken);
        Task<Category?> GetByIdAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);

        /// <param name="normalizedSearch">Already normalized (see <c>SearchText.Normalize</c>); null or empty means no filter.</param>
        Task<PagedList<Category>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, string? normalizedSearch, SortDirection sortDirection, CancellationToken cancellationToken);

        /// <summary>Case- and accent-insensitive: compares <see cref="CategoryName.NormalizedValue"/>.</summary>
        Task<bool> ExistsByNameAsync(UserId userId, CategoryName name, CategoryId? ignoredCategoryId, CancellationToken cancellationToken);
        Task UpdateAsync(Category category, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);
    }
}
