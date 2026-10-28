using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class GameItemPurchaseConfiguration : IEntityTypeConfiguration<GameItemPurchase>
    {
        public void Configure(EntityTypeBuilder<GameItemPurchase> builder)
        {
            builder.HasKey(purchase => purchase.Id);

            builder.Property(purchase => purchase.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new GameItemPurchaseId(id));

            builder.Property(purchase => purchase.Kind).IsRequired();
            builder.Property(purchase => purchase.CostPaid).IsRequired();
            builder.Property(purchase => purchase.HpRestored).IsRequired();
            builder.Property(purchase => purchase.PurchasedOn).IsRequired();
            builder.Property(purchase => purchase.PurchasedAt).IsRequired();
            builder.Property(purchase => purchase.UndoneAt);

            builder.Ignore(purchase => purchase.IsUndone);

            // The limits count a user's purchases from a day (today, or the week's Monday); the shop lists today's.
            builder.HasIndex(purchase => new { purchase.UserId, purchase.PurchasedOn });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(purchase => purchase.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
