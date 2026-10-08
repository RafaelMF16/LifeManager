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

            builder.Ignore(reward => reward.IsArchived);

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
        }
    }
}
