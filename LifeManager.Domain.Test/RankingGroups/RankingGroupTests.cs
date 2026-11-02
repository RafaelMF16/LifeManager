using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.RankingGroups
{
    public class RankingGroupTests
    {
        private static readonly UserId Owner = new(1);
        private static readonly UserId Other = new(2);
        private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 11, 4);

        private static RankingGroup Group()
            => RankingGroup.Create("Família", Owner, InviteCode.Generate(), Now, Today).Value!;

        [Fact]
        public void Create_ShouldTrimTheName()
        {
            var result = RankingGroup.Create("  Família  ", Owner, InviteCode.Generate(), Now, Today);

            Assert.True(result.IsSuccess);
            Assert.Equal("Família", result.Value!.Name.Value);
            Assert.Equal(Owner, result.Value.OwnerUserId);
            Assert.Equal(Today, result.Value.CreatedOn);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void Create_ShouldRequireAName(string? name)
        {
            Assert.Equal(RankingGroupErrors.NameIsNullOrWhiteSpace, RankingGroup.Create(name, Owner, InviteCode.Generate(), Now, Today).Error);
        }

        [Fact]
        public void Create_ShouldRefuseANameLongerThan40()
        {
            Assert.True(RankingGroup.Create(new string('a', 40), Owner, InviteCode.Generate(), Now, Today).IsSuccess);
            Assert.Equal(RankingGroupErrors.NameTooLong, RankingGroup.Create(new string('a', 41), Owner, InviteCode.Generate(), Now, Today).Error);
        }

        [Fact]
        public void Rename_ShouldOnlyBeDoneByTheOwner()
        {
            var group = Group();

            Assert.Equal(RankingGroupErrors.NotOwner, group.Rename("Trabalho", Other).Error);
            Assert.True(group.Rename("Trabalho", Owner).IsSuccess);
            Assert.Equal("Trabalho", group.Name.Value);
        }

        [Fact]
        public void ChangeInviteCode_ShouldOnlyBeDoneByTheOwner()
        {
            var group = Group();
            var code = InviteCode.Generate();

            Assert.Equal(RankingGroupErrors.NotOwner, group.ChangeInviteCode(code, Other).Error);
            Assert.True(group.ChangeInviteCode(code, Owner).IsSuccess);
            Assert.Equal(code, group.InviteCode);
        }
    }

    public class InviteCodeTests
    {
        [Fact]
        public void Generate_ShouldUseTheAlphabetOnly()
        {
            for (var i = 0; i < 50; i++)
            {
                var code = InviteCode.Generate();
                Assert.Equal(InviteCode.Length, code.Value.Length);
                Assert.All(code.Value, c => Assert.Contains(c, InviteCode.Alphabet));
            }
        }

        [Theory]
        [InlineData("abcd2345", "ABCD2345")]
        [InlineData("  ABCD2345 ", "ABCD2345")]
        public void TryParse_ShouldIgnoreCaseAndSpaces(string value, string expected)
        {
            Assert.True(InviteCode.TryParse(value, out var code));
            Assert.Equal(expected, code.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ABCD234")]
        [InlineData("ABCD23456")]
        [InlineData("ABCD2340")] // 0 is not in the alphabet
        [InlineData("ABCD234I")]
        public void TryParse_ShouldRefuseWhatCannotBeACode(string? value)
        {
            Assert.False(InviteCode.TryParse(value, out _));
        }
    }

    public class RankingGroupMembershipTests
    {
        private static readonly UserId Owner = new(1);
        private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        private static RankingGroup Group()
            => RankingGroup.FromPersistence(1, "Família", Owner.Value, "ABCD2345", Now, new DateOnly(2026, 11, 4));

        private static RankingGroupMember Member(int id, int userId, int minutesAfter)
            => RankingGroupMember.FromPersistence(id, 1, userId, Now.AddMinutes(minutesAfter), new DateOnly(2026, 11, 4));

        [Fact]
        public void CanCreate_ShouldRefuseTheSixthGroup()
        {
            Assert.True(RankingGroupMembership.CanCreate(4).IsSuccess);
            Assert.Equal(RankingGroupErrors.TooManyGroups, RankingGroupMembership.CanCreate(5).Error);
        }

        [Fact]
        public void CanJoin_ShouldCheckMembershipThenSizeThenTheUsersGroups()
        {
            Assert.True(RankingGroupMembership.CanJoin(4, 19, false).IsSuccess);
            Assert.Equal(RankingGroupErrors.AlreadyMember, RankingGroupMembership.CanJoin(5, 20, true).Error);
            Assert.Equal(RankingGroupErrors.GroupFull, RankingGroupMembership.CanJoin(5, 20, false).Error);
            Assert.Equal(RankingGroupErrors.TooManyGroups, RankingGroupMembership.CanJoin(5, 3, false).Error);
        }

        [Fact]
        public void OwnerAfterLeaving_ShouldKeepTheOwner_WhenSomeoneElseLeaves()
        {
            var members = new[] { Member(1, 1, 0), Member(2, 2, 1), Member(3, 3, 2) };

            Assert.Equal(Owner, RankingGroupMembership.OwnerAfterLeaving(Group(), members, new UserId(2)));
        }

        [Fact]
        public void OwnerAfterLeaving_ShouldPassItToTheLongestStandingMember_WhenTheOwnerLeaves()
        {
            var members = new[] { Member(1, 1, 0), Member(3, 3, 5), Member(2, 2, 5), Member(4, 4, 9) };

            Assert.Equal(new UserId(2), RankingGroupMembership.OwnerAfterLeaving(Group(), members, Owner));
        }

        [Fact]
        public void OwnerAfterLeaving_ShouldBeNull_WhenTheLastMemberLeaves()
        {
            Assert.Null(RankingGroupMembership.OwnerAfterLeaving(Group(), [Member(1, 1, 0)], Owner));
        }
    }
}
