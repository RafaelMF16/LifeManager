using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.ValueObjects;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;
        private const int PercentPrecision = 5;
        private const int PercentScale = 2;

        public void Configure(EntityTypeBuilder<BudgetPlan> builder)
        {
            builder.HasKey(plan => plan.Id);

            builder.Property(plan => plan.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new BudgetPlanId(id));

            builder.Property(plan => plan.MonthStart)
                .IsRequired();

            builder.Property(plan => plan.Total)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(total => total.Value, total => BudgetTotal.FromPersistence(total));

            builder.Ignore(plan => plan.Month);
            builder.Ignore(plan => plan.InvestmentSlice);

            // One plan per user and month. Its UserId prefix also serves the per-user period reads.
            builder.HasIndex(plan => new { plan.UserId, plan.MonthStart })
                .IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(plan => plan.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // The slices live in their own table so each can reference its category; saving a plan replaces them all.
            builder.OwnsMany(plan => plan.Slices, slices =>
            {
                slices.ToTable("BudgetPlanSlices");

                slices.WithOwner().HasForeignKey("BudgetPlanId");
                slices.Property<int>("Id").ValueGeneratedOnAdd();
                slices.HasKey("Id");

                slices.Property(slice => slice.Type)
                    .IsRequired();

                slices.Property(slice => slice.Percent)
                    .IsRequired()
                    .HasPrecision(PercentPrecision, PercentScale)
                    .HasConversion(percent => percent.Value, percent => SlicePercent.FromPersistence(percent));

                // A category has one slice per plan. The investment slice has no category, so this index leaves it
                // out (NULLs are distinct); one investment slice per plan is a domain rule.
                slices.HasIndex("BudgetPlanId", nameof(BudgetSlice.CategoryId))
                    .IsUnique();

                // A slice means nothing without its category, so deleting the category deletes its slices.
                slices.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey(slice => slice.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Navigation(plan => plan.Slices)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
