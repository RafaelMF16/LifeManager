using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
    {
        public void Configure(EntityTypeBuilder<PlayerProfile> builder)
        {
            builder.HasKey(profile => profile.Id);

            builder.Property(profile => profile.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new PlayerProfileId(id));

            builder.Property(profile => profile.Level).IsRequired();
            builder.Property(profile => profile.TotalXp).IsRequired();
            builder.Property(profile => profile.Hp).IsRequired();
            builder.Property(profile => profile.MaxHp).IsRequired();
            builder.Property(profile => profile.Coins).IsRequired();
            builder.Property(profile => profile.StreakFreezes).IsRequired();

            // The default lets PlayerWallet's raw insert leave the appearance out. EF itself always sends the value: the
            // domain never holds the sentinel 0, which isn't an avatar.
            builder.Property(profile => profile.Avatar)
                .IsRequired()
                .HasDefaultValue(PlayerAvatar.Cat)
                .HasSentinel((PlayerAvatar)0);
            builder.Property(profile => profile.Title);

            builder.Ignore(profile => profile.XpInLevel);
            builder.Ignore(profile => profile.XpToNextLevel);

            // One profile per user; also the conflict target of PlayerWallet's insert-if-missing.
            builder.HasIndex(profile => profile.UserId)
                .IsUnique();

            builder.HasOne<User>()
                .WithOne()
                .HasForeignKey<PlayerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
