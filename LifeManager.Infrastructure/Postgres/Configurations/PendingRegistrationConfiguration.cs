using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class PendingRegistrationConfiguration : IEntityTypeConfiguration<PendingRegistration>
    {
        public void Configure(EntityTypeBuilder<PendingRegistration> builder)
        {
            builder.HasKey(pending => pending.Id);

            builder.Property(pending => pending.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new PendingRegistrationId(id));

            // Same limits as Users.Name and Users.Email, which receive these values.
            builder.Property(pending => pending.Name)
                .IsRequired()
                .HasMaxLength(100)
                .HasConversion(name => name.Value, name => UserName.FromPersistence(name));

            builder.Property(pending => pending.Email)
                .IsRequired()
                .HasMaxLength(255)
                .HasConversion(email => email.Value, email => Email.FromPersistence(email));

            builder.HasIndex(pending => pending.Email)
                .IsUnique();

            builder.Property(pending => pending.Language)
                .IsRequired();

            builder.Property(pending => pending.Theme)
                .IsRequired();

            builder.Property(pending => pending.TokenHash)
                .IsRequired()
                .HasMaxLength(64)
                .HasConversion(tokenHash => tokenHash.Value, tokenHash => EmailTokenHash.FromPersistence(tokenHash));

            builder.HasIndex(pending => pending.TokenHash)
                .IsUnique();

            builder.Property(pending => pending.CreatedAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(pending => pending.ExpiresAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.HasIndex(pending => pending.ExpiresAt);
        }
    }
}
