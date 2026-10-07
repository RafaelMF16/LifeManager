using LifeManager.Domain.Habits;
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
