using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
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

        public async Task<IReadOnlyList<Category>> GetAllByUserIdAsync(UserId userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Categories
                .AsNoTracking()
                .Where(category => category.UserId == userId)
                .OrderBy(category => category.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(UserId userId, CategoryName name, CategoryId? ignoredCategoryId, CancellationToken cancellationToken)
        {
            var query = _dbContext.Categories
                .Where(category => category.UserId == userId && category.Name == name);

            if (ignoredCategoryId is not null)
                query = query.Where(category => category.Id != ignoredCategoryId);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task UpdateAsync(Category category, CancellationToken cancellationToken)
        {
            await _dbContext.Categories
                .Where(storedCategory => storedCategory.Id == category.Id && storedCategory.UserId == category.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(storedCategory => storedCategory.Name, category.Name), cancellationToken);
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
