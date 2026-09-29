using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasKey(category => category.Id);

            builder.Property(category => category.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new CategoryId(id));

            builder.Property(category => category.Name)
                .IsRequired()
                .HasMaxLength(CategoryName.MaxLength)
                .HasConversion(name => name.Value, name => CategoryName.FromPersistence(name));

            builder.HasIndex(category => new { category.UserId, category.Name })
                .IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(category => category.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
