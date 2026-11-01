using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.RankingGroups
{
    public class RankingGroupMember
    {
        public RankingGroupMemberId? Id { get; private set; }
        public RankingGroupId GroupId { get; }
        public UserId UserId { get; }
        public DateTimeOffset JoinedAt { get; }

        /// <summary>The game day they joined: a week only counts for their achievements when they were in from its Monday.</summary>
        public DateOnly JoinedOn { get; }

        private RankingGroupMember(RankingGroupId groupId, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn)
        {
            GroupId = groupId;
            UserId = userId;
            JoinedAt = joinedAt;
            JoinedOn = joinedOn;
        }

        public static RankingGroupMember Create(RankingGroupId groupId, UserId userId, DateTimeOffset joinedAt, DateOnly joinedOn)
            => new(groupId, userId, joinedAt, joinedOn);

        public void AssignId(int id)
        {
            Id = new RankingGroupMemberId(id);
        }

        internal static RankingGroupMember FromPersistence(int id, int idGroup, int idUser, DateTimeOffset joinedAt, DateOnly joinedOn)
        {
            var member = new RankingGroupMember(new RankingGroupId(idGroup), new UserId(idUser), joinedAt, joinedOn);
            member.AssignId(id);

            return member;
        }
    }
}
