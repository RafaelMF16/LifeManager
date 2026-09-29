using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category> AddAsync(Category category, CancellationToken cancellationToken);
        Task<Category?> GetByIdAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);
        Task<IReadOnlyList<Category>> GetAllByUserIdAsync(UserId userId, CancellationToken cancellationToken);
        Task<bool> ExistsByNameAsync(UserId userId, CategoryName name, CategoryId? ignoredCategoryId, CancellationToken cancellationToken);
        Task UpdateAsync(Category category, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken);
    }
}
