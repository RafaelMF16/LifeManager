using LifeManager.Domain.Categories;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class StatementImportRowConfiguration : IEntityTypeConfiguration<StatementImportRow>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<StatementImportRow> builder)
        {
            builder.HasKey(row => row.Id);

            builder.Property(row => row.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new StatementImportRowId(id));

            builder.Property(row => row.Date).IsRequired();

            builder.Property(row => row.OriginalDescription)
                .IsRequired()
                .HasMaxLength(StatementImportRow.OriginalDescriptionMaxLength);

            builder.Property(row => row.MerchantKey)
                .IsRequired()
                .HasMaxLength(MerchantKey.MaxLength);

            builder.Property(row => row.Description)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength)
                .HasConversion(description => description.Value, description => TransactionDescription.FromPersistence(description));

            builder.Property(row => row.NormalizedDescription)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength);

            builder.Property(row => row.Amount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(amount => amount.Value, amount => TransactionAmount.FromPersistence(amount));

            builder.Property(row => row.SignedAmount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale);

            builder.Property(row => row.Type).IsRequired();
            builder.Property(row => row.Included).IsRequired();
            builder.Property(row => row.ExternalId).HasMaxLength(ImportExternalId.Length);
            builder.Property(row => row.DuplicateOfTransactionId);
            builder.Property(row => row.DuplicateOfMonthlySummaryId);
            builder.Property(row => row.SuggestionSource).IsRequired();
            builder.Property(row => row.SuggestionConfidence);
            builder.Property(row => row.SuggestionUses).IsRequired();
            builder.Property(row => row.EditedByUser).IsRequired();

            builder.Ignore(row => row.IsDuplicate);
            builder.Ignore(row => row.Month);

            // The default listing (by date) and the merchant lookups of "apply to similar" stay inside one import.
            builder.HasIndex(row => new { row.StatementImportId, row.Date });
            builder.HasIndex(row => new { row.StatementImportId, row.MerchantKey });

            builder.HasOne<StatementImport>()
                .WithMany()
                .HasForeignKey(row => row.StatementImportId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting a category only un-files the draft rows that used it.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(row => row.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
