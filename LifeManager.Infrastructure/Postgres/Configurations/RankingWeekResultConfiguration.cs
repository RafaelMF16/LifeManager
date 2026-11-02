using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.ValueObjects;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RankingWeekResultConfiguration : IEntityTypeConfiguration<RankingWeekResult>
    {
        public void Configure(EntityTypeBuilder<RankingWeekResult> builder)
        {
            builder.HasKey(result => result.Id);

            builder.Property(result => result.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RankingWeekResultId(id));

            builder.Property(result => result.GroupId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new RankingGroupId(id));

            builder.Property(result => result.WeekStart).IsRequired();
            builder.Property(result => result.Points).IsRequired();
            builder.Property(result => result.PerfectDays).IsRequired();
            builder.Property(result => result.KeptDays).IsRequired();
            builder.Property(result => result.Position).IsRequired();
            builder.Property(result => result.Eligible).IsRequired();
            builder.Property(result => result.MemberCount).IsRequired();
            builder.Property(result => result.EligibleCount).IsRequired();
            builder.Property(result => result.ClosedAt).IsRequired();

            // A week is stored once: a second close running at the same time fails as a whole. Also serves the group's
            // week reads.
            builder.HasIndex(result => new { result.GroupId, result.WeekStart, result.UserId }).IsUnique();

            // The ranking achievements count a player's weeks.
            builder.HasIndex(result => result.UserId);

            builder.HasOne<RankingGroup>()
                .WithMany()
                .HasForeignKey(result => result.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(result => result.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
