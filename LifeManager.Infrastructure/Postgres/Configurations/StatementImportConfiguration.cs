using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class StatementImportConfiguration : IEntityTypeConfiguration<StatementImport>
    {
        public void Configure(EntityTypeBuilder<StatementImport> builder)
        {
            builder.HasKey(statementImport => statementImport.Id);

            builder.Property(statementImport => statementImport.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new StatementImportId(id));

            builder.Property(statementImport => statementImport.FileName)
                .IsRequired()
                .HasMaxLength(StatementImport.FileNameMaxLength);

            builder.Property(statementImport => statementImport.Source).IsRequired();
            builder.Property(statementImport => statementImport.Status).IsRequired();
            builder.Property(statementImport => statementImport.RowCount).IsRequired();
            builder.Property(statementImport => statementImport.FirstDate).IsRequired();
            builder.Property(statementImport => statementImport.LastDate).IsRequired();
            builder.Property(statementImport => statementImport.CreatedAt).IsRequired();
            builder.Property(statementImport => statementImport.CommittedAt);
            builder.Property(statementImport => statementImport.ImportedCount);
            builder.Property(statementImport => statementImport.CreatedMonthCount);

            builder.Ignore(statementImport => statementImport.IsDraft);

            // Serves the history listing (drafts first, then newest first).
            builder.HasIndex(statementImport => new { statementImport.UserId, statementImport.Status, statementImport.CreatedAt });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(statementImport => statementImport.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
