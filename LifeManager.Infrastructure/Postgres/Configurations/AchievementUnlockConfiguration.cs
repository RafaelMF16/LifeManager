using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class AchievementUnlockConfiguration : IEntityTypeConfiguration<AchievementUnlock>
    {
        public void Configure(EntityTypeBuilder<AchievementUnlock> builder)
        {
            builder.HasKey(unlock => unlock.Id);

            builder.Property(unlock => unlock.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new AchievementUnlockId(id));

            builder.Property(unlock => unlock.Code).IsRequired();
            builder.Property(unlock => unlock.UnlockedOn).IsRequired();
            builder.Property(unlock => unlock.UnlockedAt).IsRequired();
            builder.Property(unlock => unlock.SeenAt);

            builder.Ignore(unlock => unlock.IsSeen);

            // Once per player: the last guard against two syncs paying the same achievement.
            builder.HasIndex(unlock => new { unlock.UserId, unlock.Code }).IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(unlock => unlock.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
