using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(refreshToken => refreshToken.Id);

            builder.Property(refreshToken => refreshToken.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RefreshTokenId(id));

            builder.Property(refreshToken => refreshToken.TokenHash)
                .IsRequired()
                .HasConversion(tokenHash => tokenHash!.Value, tokenHash => RefreshTokenHash.FromPersistence(tokenHash));

            builder.Property(refreshToken => refreshToken.CreatedAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(refreshToken => refreshToken.ExpiresAt)
                .IsRequired()
                .ValueGeneratedNever();

            builder.Property(refreshToken => refreshToken.IsRevoked)
                .IsRequired();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(refreshToken => refreshToken.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}