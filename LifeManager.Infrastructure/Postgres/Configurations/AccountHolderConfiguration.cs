using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.AccountHolders.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class AccountHolderConfiguration : IEntityTypeConfiguration<AccountHolder>
    {
        public void Configure(EntityTypeBuilder<AccountHolder> builder)
        {
            builder.HasKey(accountHolder => accountHolder.Id);

            builder.Property(accountHolder => accountHolder.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new AccountHolderId(id));

            builder.Property(accountHolder => accountHolder.Name)
                .IsRequired()
                .HasMaxLength(AccountHolderName.MaxLength)
                .HasConversion(name => name.Value, name => AccountHolderName.FromPersistence(name));

            builder.Property(accountHolder => accountHolder.Position)
                .IsRequired();

            // Serves the read (by user, in order) and keeps one name per place of the list.
            builder.HasIndex(accountHolder => new { accountHolder.UserId, accountHolder.Position })
                .IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(accountHolder => accountHolder.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
