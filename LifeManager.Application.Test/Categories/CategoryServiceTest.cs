using LifeManager.Application.Categories.DTOs;
using LifeManager.Application.Categories.Services;
using LifeManager.Application.Test.Categories.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Categories
{
    [Collection("ApplicationServices")]
    public class CategoryServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        private readonly CategoryService _categoryService;
        private readonly CategoryRepositoryMock _categoryRepository;

        public CategoryServiceTest()
        {
            _categoryService = ServiceProvider.GetRequiredService<CategoryService>();
            _categoryRepository = (CategoryRepositoryMock)ServiceProvider.GetRequiredService<ICategoryRepository>();

            CategorySingleton.Instance.Clear();
        }

        [Fact]
        public async Task CreateAsync_ShouldAddCategory_WhenNameIsValid()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
            var category = Assert.Single(CategorySingleton.Instance);
            Assert.Equal(result.Value.Id, category.Id!.Value);
            Assert.Equal(FirstUserId, category.UserId);
            Assert.Equal("Food", category.Name.Value);
        }

        [Fact]
        public async Task CreateAsync_ShouldTrimName_WhenNameHasSurroundingWhiteSpace()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto("  Food  "), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenUserAlreadyHasCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Single(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenNameOnlyDiffersBySurroundingWhiteSpace()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto(" Food "), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldAddCategory_WhenOtherUserHasCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), SecondUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, CategorySingleton.Instance.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_ShouldReturnValidationError_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(name!), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Empty(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnValidationError_WhenNameIsTooLong()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(new string('a', 51)), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameTooLong, result.Error);
            Assert.Empty(CategorySingleton.Instance);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCategory_WhenCategoryBelongsToUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.GetByIdAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(createdCategory, result.Value);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.GetByIdAsync(99, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.GetByIdAsync(createdCategory.Id, SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnEmptyList_WhenUserHasNoCategories()
        {
            await CreateCategory("Food", SecondUserId);

            var categories = await _categoryService.GetAllAsync(FirstUserId, CancellationToken.None);

            Assert.Empty(categories);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnOnlyUserCategories_WhenOtherUsersHaveCategories()
        {
            await CreateCategory("Food", FirstUserId);
            await CreateCategory("Health", SecondUserId);
            await CreateCategory("Salary", FirstUserId);

            var categories = await _categoryService.GetAllAsync(FirstUserId, CancellationToken.None);

            Assert.Equal(["Food", "Salary"], categories.Select(category => category.Name));
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnCategoriesOrderedByName_WhenUserHasCategories()
        {
            await CreateCategory("Salary", FirstUserId);
            await CreateCategory("Food", FirstUserId);
            await CreateCategory("Health", FirstUserId);

            var categories = await _categoryService.GetAllAsync(FirstUserId, CancellationToken.None);

            Assert.Equal(["Food", "Health", "Salary"], categories.Select(category => category.Name));
        }

        [Fact]
        public async Task UpdateAsync_ShouldRenameCategory_WhenNameIsValid()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new CategoryResponseDto(createdCategory.Id, "Groceries"), result.Value);
            var category = Assert.Single(CategorySingleton.Instance);
            Assert.Equal("Groceries", category.Name.Value);
        }

        [Theory]
        [InlineData("Food")]
        [InlineData("  Food  ")]
        public async Task UpdateAsync_ShouldSucceedWithoutWriting_WhenNameIsTheSameAsCurrentName(string name)
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            var existsByNameCallCountBeforeUpdate = _categoryRepository.ExistsByNameCallCount;

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto(name), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(createdCategory, result.Value);
            Assert.Equal(existsByNameCallCountBeforeUpdate, _categoryRepository.ExistsByNameCallCount);
            Assert.Equal(0, _categoryRepository.UpdateCallCount);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldWrite_WhenNameChanged()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), FirstUserId, CancellationToken.None);

            Assert.Equal(1, _categoryRepository.UpdateCallCount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnConflict_WhenUserHasOtherCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);
            var categoryToRename = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.UpdateAsync(categoryToRename.Id, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
            Assert.Equal("Health", CategorySingleton.Instance.Single(category => category.Id!.Value == categoryToRename.Id).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRenameCategory_WhenOtherUserHasCategoryWithSameName()
        {
            await CreateCategory("Food", SecondUserId);
            var createdCategory = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateAsync_ShouldKeepOriginalName_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto(name!), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.UpdateAsync(99, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFoundAndKeepName_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveCategory_WhenCategoryBelongsToUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            var remainingCategory = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.DeleteAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(remainingCategory.Id, Assert.Single(CategorySingleton.Instance).Id!.Value);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.DeleteAsync(99, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFoundAndKeepCategory_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.DeleteAsync(createdCategory.Id, SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Single(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowSameName_WhenPreviousCategoryWasDeleted()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            await _categoryService.DeleteAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        private async Task<CategoryResponseDto> CreateCategory(string name, UserId userId)
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(name), userId, CancellationToken.None);

            return result.Value!;
        }
    }
}
