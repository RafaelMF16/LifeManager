using LifeManager.Domain.Categories;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class CategorizationRuleConfiguration : IEntityTypeConfiguration<CategorizationRule>
    {
        public void Configure(EntityTypeBuilder<CategorizationRule> builder)
        {
            builder.HasKey(rule => rule.Id);

            builder.Property(rule => rule.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new CategorizationRuleId(id));

            builder.Property(rule => rule.MerchantKey)
                .IsRequired()
                .HasMaxLength(MerchantKey.MaxLength);

            builder.Property(rule => rule.Type).IsRequired();
            builder.Property(rule => rule.Action).IsRequired();
            builder.Property(rule => rule.Hits).IsRequired();
            builder.Property(rule => rule.LastUsedOn).IsRequired();

            // One row per merchant and decision, which the confirmation's upsert (ON CONFLICT) adds hits to. NULLS NOT
            // DISTINCT so an "ignore" (no category) is unique too. The UserId prefix also serves the per-user read.
            builder.HasIndex(rule => new { rule.UserId, rule.MerchantKey, rule.Type, rule.Action, rule.CategoryId })
                .IsUnique()
                .AreNullsDistinct(false);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(rule => rule.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // A rule means nothing without its category.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(rule => rule.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
