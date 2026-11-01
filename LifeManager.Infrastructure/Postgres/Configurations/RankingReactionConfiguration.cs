using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.ValueObjects;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RankingReactionConfiguration : IEntityTypeConfiguration<RankingReaction>
    {
        public void Configure(EntityTypeBuilder<RankingReaction> builder)
        {
            builder.HasKey(reaction => reaction.Id);

            builder.Property(reaction => reaction.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RankingReactionId(id));

            builder.Property(reaction => reaction.GroupId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RankingGroupId(id));

            builder.Property(reaction => reaction.FromMemberId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RankingGroupMemberId(id));

            builder.Property(reaction => reaction.ToMemberId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RankingGroupMemberId(id));

            builder.Property(reaction => reaction.WeekStart).IsRequired();
            builder.Property(reaction => reaction.Kind).IsRequired();
            builder.Property(reaction => reaction.CreatedAt).IsRequired();

            // One reaction per pair, week and group: the upsert's conflict target. Also serves the week's read.
            builder.HasIndex(reaction => new { reaction.GroupId, reaction.WeekStart, reaction.FromMemberId, reaction.ToMemberId }).IsUnique();

            builder.HasIndex(reaction => reaction.FromMemberId);
            builder.HasIndex(reaction => reaction.ToMemberId);

            builder.HasOne<RankingGroup>()
                .WithMany()
                .HasForeignKey(reaction => reaction.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // Leaving the group (the member's row) takes the reactions sent and received with it.
            builder.HasOne<RankingGroupMember>()
                .WithMany()
                .HasForeignKey(reaction => reaction.FromMemberId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<RankingGroupMember>()
                .WithMany()
                .HasForeignKey(reaction => reaction.ToMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
