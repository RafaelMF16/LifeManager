using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categories
{
    public class Category
    {
        public CategoryId? Id { get; private set; }
        public UserId UserId { get; }
        public CategoryName Name { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="CategoryName.NormalizedValue"/>, kept as its own column so it can be
        /// indexed (trigram search, per-user uniqueness). Always derived from <see cref="Name"/>.
        /// </summary>
        public string NormalizedName { get; private set; }

        private Category(UserId userId, CategoryName name)
        {
            UserId = userId;
            Name = name;
            NormalizedName = name.NormalizedValue;
        }

        public static Result<Category> Create(int idUser, string name)
        {
            var userId = new UserId(idUser);

            return CategoryName.Create(name)
                .Map(categoryName => new Category(userId, categoryName));
        }

        public Result<Category> Rename(string name)
        {
            return CategoryName.Create(name)
                .Map(categoryName =>
                {
                    Name = categoryName;
                    NormalizedName = categoryName.NormalizedValue;
                    return this;
                });
        }

        public void AssignId(int id)
        {
            Id = new CategoryId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Category other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
