using LifeManager.Domain.Push;
using LifeManager.Domain.Push.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
    {
        public void Configure(EntityTypeBuilder<PushSubscription> builder)
        {
            builder.HasKey(subscription => subscription.Id);

            builder.Property(subscription => subscription.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new PushSubscriptionId(id));

            builder.Property(subscription => subscription.Endpoint).IsRequired().HasMaxLength(PushSubscription.EndpointMaxLength);
            builder.Property(subscription => subscription.P256dh).IsRequired().HasMaxLength(PushSubscription.KeyMaxLength);
            builder.Property(subscription => subscription.Auth).IsRequired().HasMaxLength(PushSubscription.KeyMaxLength);
            builder.Property(subscription => subscription.Label).HasMaxLength(PushSubscription.LabelMaxLength);
            builder.Property(subscription => subscription.CreatedAt).IsRequired();
            builder.Property(subscription => subscription.LastSuccessAt);

            // One row per device: the upsert's ON CONFLICT target (another account on the same device takes it over).
            builder.HasIndex(subscription => subscription.Endpoint).IsUnique();

            // The job reads a user's devices.
            builder.HasIndex(subscription => subscription.UserId);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(subscription => subscription.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
