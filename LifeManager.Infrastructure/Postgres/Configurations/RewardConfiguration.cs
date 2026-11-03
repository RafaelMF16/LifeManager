using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RewardConfiguration : IEntityTypeConfiguration<Reward>
    {
        public void Configure(EntityTypeBuilder<Reward> builder)
        {
            builder.HasKey(reward => reward.Id);

            builder.Property(reward => reward.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RewardId(id));

            builder.Property(reward => reward.Name)
                .IsRequired()
                .HasMaxLength(RewardName.MaxLength)
                .HasConversion(name => name.Value, name => RewardName.FromPersistence(name));

            builder.Property(reward => reward.NormalizedName)
                .IsRequired()
                .HasMaxLength(RewardName.MaxLength);

            builder.Property(reward => reward.Cost).IsRequired();
            builder.Property(reward => reward.Icon).HasMaxLength(Reward.IconMaxLength);
            builder.Property(reward => reward.CreatedAt).IsRequired();
            builder.Property(reward => reward.ArchivedAt);

            // The real expense the reward stands for: both set or both null, until the category is deleted.
            builder.Property(reward => reward.ExpenseCategoryId)
                .HasConversion(id => id!.Value, id => new CategoryId(id));
            builder.Property(reward => reward.ExpenseAmount).HasPrecision(14, 2);

            builder.Ignore(reward => reward.IsArchived);
            builder.Ignore(reward => reward.HasExpense);

            // Names are unique among the user's active rewards only: an archived reward frees its name.
            builder.HasIndex(reward => new { reward.UserId, reward.NormalizedName })
                .IsUnique()
                .HasFilter("\"ArchivedAt\" IS NULL");

            builder.HasIndex(reward => new { reward.UserId, reward.ArchivedAt });

            // Lets LIKE '%term%' on NormalizedName use an index instead of scanning the table.
            builder.HasIndex(reward => reward.NormalizedName)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(reward => reward.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting the category leaves the reward without an expense instead of blocking the deletion.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(reward => reward.ExpenseCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
