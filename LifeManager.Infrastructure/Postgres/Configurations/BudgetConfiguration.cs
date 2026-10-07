using LifeManager.Domain.Budgets;
using LifeManager.Domain.Budgets.ValueObjects;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<Budget> builder)
        {
            builder.HasKey(budget => budget.Id);

            builder.Property(budget => budget.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new BudgetId(id));

            builder.Property(budget => budget.Type)
                .IsRequired();

            builder.Property(budget => budget.Amount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(amount => amount.Value, amount => BudgetAmount.FromPersistence(amount));

            builder.Property(budget => budget.EffectiveFrom)
                .IsRequired();

            builder.Property(budget => budget.EffectiveTo);

            builder.Ignore(budget => budget.FromMonth);
            builder.Ignore(budget => budget.ToMonth);

            // One version of a goal per starting month. NULLS NOT DISTINCT so the month-total goal (no category)
            // is unique too. Its UserId prefix also serves the per-user period reads.
            builder.HasIndex(budget => new { budget.UserId, budget.Type, budget.CategoryId, budget.EffectiveFrom })
                .IsUnique()
                .AreNullsDistinct(false);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(budget => budget.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // A goal means nothing without its category, so deleting the category deletes its goals.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(budget => budget.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
