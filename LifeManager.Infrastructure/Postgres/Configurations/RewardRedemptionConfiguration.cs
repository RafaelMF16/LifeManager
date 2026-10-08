using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RewardRedemptionConfiguration : IEntityTypeConfiguration<RewardRedemption>
    {
        public void Configure(EntityTypeBuilder<RewardRedemption> builder)
        {
            builder.HasKey(redemption => redemption.Id);

            builder.Property(redemption => redemption.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RewardRedemptionId(id));

            builder.Property(redemption => redemption.RewardId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RewardId(id));

            builder.Property(redemption => redemption.RewardName)
                .IsRequired()
                .HasMaxLength(RewardName.MaxLength);

            builder.Property(redemption => redemption.RewardIcon).HasMaxLength(Reward.IconMaxLength);
            builder.Property(redemption => redemption.CostPaid).IsRequired();
            builder.Property(redemption => redemption.RedeemedOn).IsRequired();
            builder.Property(redemption => redemption.RedeemedAt).IsRequired();
            builder.Property(redemption => redemption.UndoneAt);

            builder.Ignore(redemption => redemption.IsUndone);

            // The history lists a user's redemptions newest first.
            builder.HasIndex(redemption => new { redemption.UserId, redemption.RedeemedAt, redemption.Id });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(redemption => redemption.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Rewards are archived, never deleted, so the history always has its reward; a user deletion cascades
            // through both tables.
            builder.HasOne<Reward>()
                .WithMany()
                .HasForeignKey(redemption => redemption.RewardId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
