using LifeManager.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class EmailCooldownConfiguration : IEntityTypeConfiguration<EmailCooldown>
    {
        public void Configure(EntityTypeBuilder<EmailCooldown> builder)
        {
            builder.HasKey(cooldown => new { cooldown.Email, cooldown.Kind });

            builder.Property(cooldown => cooldown.Email)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(cooldown => cooldown.Kind)
                .IsRequired();

            builder.Property(cooldown => cooldown.LastSentAt)
                .IsRequired();

            builder.HasIndex(cooldown => cooldown.LastSentAt);
        }
    }
}
