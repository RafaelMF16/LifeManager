using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(user => user.Id);

            builder.Property(user => user.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new UserId(id));

            builder.Property(user => user.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasConversion(name => name!.Value, name => UserName.FromPersistence(name));

            builder.Property(user => user.Email)
                .IsRequired()
                .HasMaxLength(255)
                .HasConversion(email => email!.Value, email => Email.FromPersistence(email));

            builder.HasIndex(user => user.Email)
                .IsUnique();

            builder.Property(user => user.PasswordHash)
                .IsRequired()
                .HasConversion(passwordHash => passwordHash!.Value, passwordHash => PasswordHash.FromPersistence(passwordHash));

            builder.Property(user => user.CreationDate)
                .IsRequired()
                .ValueGeneratedNever();
        }
    }
}