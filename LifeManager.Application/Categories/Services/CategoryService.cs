using LifeManager.Application.Categories.DTOs;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Categories.Services
{
    public class CategoryService(ICategoryRepository categoryRepository)
    {
        private readonly ICategoryRepository _categoryRepository = categoryRepository;

        public async Task<Result<CategoryResponseDto>> CreateAsync(CategoryDto categoryDto, UserId userId, CancellationToken cancellationToken)
        {
            var categoryResult = Category.Create(userId.Value, categoryDto.Name);
            if (!categoryResult.IsSuccess)
                return categoryResult.Error;

            var category = categoryResult.Value;

            if (await _categoryRepository.ExistsByNameAsync(userId, category.Name, null, cancellationToken))
                return CategoryErrors.NameAlreadyExists;

            await _categoryRepository.AddAsync(category, cancellationToken);

            return ToResponseDto(category);
        }

        public async Task<Result<CategoryResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(new CategoryId(id), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            return ToResponseDto(category);
        }

        public async Task<IReadOnlyList<CategoryResponseDto>> GetAllAsync(UserId userId, CancellationToken cancellationToken)
        {
            var categories = await _categoryRepository.GetAllByUserIdAsync(userId, cancellationToken);

            return [.. categories.Select(ToResponseDto)];
        }

        public async Task<Result<CategoryResponseDto>> UpdateAsync(int id, CategoryDto categoryDto, UserId userId, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(new CategoryId(id), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            var currentName = category.Name;

            var renameResult = category.Rename(categoryDto.Name);
            if (!renameResult.IsSuccess)
                return renameResult.Error;

            if (category.Name.Equals(currentName))
                return ToResponseDto(category);

            if (await _categoryRepository.ExistsByNameAsync(userId, category.Name, category.Id, cancellationToken))
                return CategoryErrors.NameAlreadyExists;

            await _categoryRepository.UpdateAsync(category, cancellationToken);

            return ToResponseDto(category);
        }

        public async Task<Result> DeleteAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var deleted = await _categoryRepository.DeleteAsync(new CategoryId(id), userId, cancellationToken);
            if (!deleted)
                return CategoryErrors.NotFound;

            return Result.Success();
        }

        private static CategoryResponseDto ToResponseDto(Category category)
            => new(category.Id!.Value, category.Name.Value);
    }
}
