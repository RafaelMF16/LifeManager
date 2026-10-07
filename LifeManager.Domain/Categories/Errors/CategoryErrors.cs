using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Categories.Errors
{
    public static class CategoryErrors
    {
        public static readonly Error NotFound = Error.NotFound("Category.NotFound", "Category not found");

        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("Category.NameIsNullOrWhiteSpace", "CategoryName is required");
        public static readonly Error NameTooLong = Error.Validation("Category.NameTooLong", "CategoryName cannot be longer than 50 characters");

        public static readonly Error NameAlreadyExists = Error.Conflict("Category.NameAlreadyExists", "Category name already exists");
        public static readonly Error InUse = Error.Conflict("Category.InUse", "Category is used by transactions or recurring transactions and cannot be deleted");
    }
}
