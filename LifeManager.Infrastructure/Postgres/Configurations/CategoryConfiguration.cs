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

            builder.Property(category => category.NormalizedName)
                .IsRequired()
                .HasMaxLength(CategoryName.MaxLength);

            builder.HasIndex(category => new { category.UserId, category.NormalizedName })
                .IsUnique();

            // Lets LIKE '%term%' on NormalizedName use an index instead of scanning the table.
            builder.HasIndex(category => category.NormalizedName)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(category => category.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
