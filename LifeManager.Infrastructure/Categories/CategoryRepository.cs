using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Categories
{
    public class CategoryRepository(LifeManagerDbContext dbContext) : ICategoryRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task<Category> AddAsync(Category category, CancellationToken cancellationToken)
        {
            _dbContext.Add(category);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return category;
        }

        public async Task<Category?> GetByIdAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Categories
                .AsNoTracking()
                .SingleOrDefaultAsync(category => category.Id == categoryId && category.UserId == userId, cancellationToken);
        }

        public async Task<PagedList<Category>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, string? normalizedSearch, SortDirection sortDirection, CancellationToken cancellationToken)
        {
            var query = _dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId);

            if (!string.IsNullOrEmpty(normalizedSearch))
            {
                var pattern = QueryablePagingExtensions.ToContainsLikePattern(normalizedSearch);
                query = query.Where(category => EF.Functions.Like(category.NormalizedName, pattern, QueryablePagingExtensions.LikeEscapeCharacter));
            }

            var orderedQuery = sortDirection == SortDirection.Desc
                ? query.OrderByDescending(category => category.NormalizedName).ThenByDescending(category => category.Id)
                : query.OrderBy(category => category.NormalizedName).ThenBy(category => category.Id);

            return await orderedQuery.ToPagedListAsync(pageRequest, cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(UserId userId, CategoryName name, CategoryId? ignoredCategoryId, CancellationToken cancellationToken)
        {
            var normalizedName = name.NormalizedValue;
            var query = _dbContext.Categories
                .Where(category => category.UserId == userId && category.NormalizedName == normalizedName);

            if (ignoredCategoryId is not null)
                query = query.Where(category => category.Id != ignoredCategoryId);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task UpdateAsync(Category category, CancellationToken cancellationToken)
        {
            await _dbContext.Categories
                .Where(storedCategory => storedCategory.Id == category.Id && storedCategory.UserId == category.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedCategory => storedCategory.Name, category.Name)
                    .SetProperty(storedCategory => storedCategory.NormalizedName, category.NormalizedName), cancellationToken);
        }

        public async Task<bool> DeleteAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            var deletedRows = await _dbContext.Categories
                .Where(category => category.Id == categoryId && category.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            return deletedRows > 0;
        }
    }
}
