using LifeManager.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
    {
        public void Configure(EntityTypeBuilder<LoginAttempt> builder)
        {
            builder.HasKey(attempt => new { attempt.Email, attempt.IpAddress });

            builder.Property(attempt => attempt.Email)
                .IsRequired()
                .HasMaxLength(320);

            // Fits an IPv6 address in text (at most 45 characters).
            builder.Property(attempt => attempt.IpAddress)
                .IsRequired()
                .HasMaxLength(45);

            builder.Property(attempt => attempt.FailedCount)
                .IsRequired();

            builder.Property(attempt => attempt.WindowStartedAt)
                .IsRequired();

            builder.HasIndex(attempt => attempt.WindowStartedAt);
        }
    }
}
