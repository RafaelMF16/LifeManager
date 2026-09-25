using LifeManager.Domain.Users;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
    {
        public void Configure(EntityTypeBuilder<UserPreferences> builder)
        {
            builder.HasKey(userPreferences => userPreferences.Id);

            builder.Property(userPreferences => userPreferences.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new UserPreferencesId(id));

            builder.Property(userPreferences => userPreferences.Theme)
                .IsRequired();

            builder.Property(userPreferences => userPreferences.Language)
                .IsRequired();

            builder.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserPreferences>(userPreferences => userPreferences.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}