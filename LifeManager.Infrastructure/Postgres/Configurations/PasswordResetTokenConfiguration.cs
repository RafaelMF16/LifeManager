using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
    {
        public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
        {
            builder.HasKey(token => token.Id);

            builder.Property(token => token.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new PasswordResetTokenId(id));

            builder.Property(token => token.TokenHash)
                .IsRequired()
                .HasMaxLength(64)
                .HasConversion(tokenHash => tokenHash.Value, tokenHash => EmailTokenHash.FromPersistence(tokenHash));

            builder.HasIndex(token => token.TokenHash)
                .IsUnique();

            builder.Property(token => token.CreatedAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(token => token.ExpiresAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.HasIndex(token => token.ExpiresAt);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
