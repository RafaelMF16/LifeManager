using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RankingGroupConfiguration : IEntityTypeConfiguration<RankingGroup>
    {
        public void Configure(EntityTypeBuilder<RankingGroup> builder)
        {
            builder.HasKey(group => group.Id);

            builder.Property(group => group.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RankingGroupId(id));

            builder.Property(group => group.Name)
                .IsRequired()
                .HasMaxLength(RankingGroupName.MaxLength)
                .HasConversion(name => name.Value, name => RankingGroupName.FromPersistence(name));

            builder.Property(group => group.InviteCode)
                .IsRequired()
                .HasMaxLength(InviteCode.Length)
                .HasConversion(code => code.Value, code => InviteCode.FromPersistence(code));

            builder.Property(group => group.CreatedAt).IsRequired();
            builder.Property(group => group.CreatedOn).IsRequired();

            // Joining looks the group up by its code; unique so a code can never open two groups.
            builder.HasIndex(group => group.InviteCode).IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(group => group.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class RankingGroupMemberConfiguration : IEntityTypeConfiguration<RankingGroupMember>
    {
        public void Configure(EntityTypeBuilder<RankingGroupMember> builder)
        {
            builder.HasKey(member => member.Id);

            builder.Property(member => member.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RankingGroupMemberId(id));

            builder.Property(member => member.GroupId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RankingGroupId(id));

            builder.Property(member => member.JoinedAt).IsRequired();
            builder.Property(member => member.JoinedOn).IsRequired();

            // Once per group; also serves the group's member list.
            builder.HasIndex(member => new { member.GroupId, member.UserId }).IsUnique();

            // A user's groups.
            builder.HasIndex(member => member.UserId);

            builder.HasOne<RankingGroup>()
                .WithMany()
                .HasForeignKey(member => member.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
