using LifeManager.Application.RankingGroups.DTOs;
using LifeManager.Application.RankingGroups.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.RankingGroups
{
    [Collection("ApplicationServices")]
    public class RankingGroupServiceTest : BaseTest
    {
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        private static readonly DateOnly Today = new(2026, 11, 4);

        private readonly RankingGroupService _service;

        public RankingGroupServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<RankingGroupService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            RankingGroupSingleton.Instance.Clear();
            RankingGroupMemberSingleton.Instance.Clear();
            RankingReactionSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            SeedUsers();
        }

        internal static void SeedUsers(int count = 25)
        {
            for (var id = 1; id <= count; id++)
            {
                var user = User.Create(id switch { 1 => "Ana", 2 => "Bia", 3 => "Caio", _ => $"User {id}" }, $"user{id}@example.com", "hash").Value!;
                user.AssignId(id);
                UserSingleton.Instance.Add(user);
            }
        }

        private async Task<RankingGroupDetailsDto> CreateAsync(UserId owner, string name = "Família")
            => (await _service.CreateAsync(new RankingGroupRequestDto(name), owner, CancellationToken.None)).Value!;

        private async Task<RankingGroupDetailsDto> JoinAsync(RankingGroupDetailsDto group, UserId userId)
            => (await _service.JoinAsync(group.InviteCode, userId, CancellationToken.None)).Value!;

        [Fact]
        public async Task CreateAsync_ShouldMakeTheCreatorTheOwnerAndFirstMember()
        {
            PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, Ana.Value, 0, 100, 100, 0, 0, PlayerAvatar.Bird));

            var result = await _service.CreateAsync(new RankingGroupRequestDto("  Família "), Ana, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var group = result.Value!;
            Assert.Equal("Família", group.Name);
            Assert.True(group.IsOwner);
            Assert.Equal(Today, group.CreatedOn);
            Assert.Equal(8, group.InviteCode.Length);
            var member = Assert.Single(group.Members);
            Assert.Equal("Ana", member.Name);
            Assert.Equal(PlayerAvatar.Bird, member.Avatar);
            Assert.True(member.IsOwner);
            Assert.True(member.IsMe);
            Assert.Equal(Today, member.JoinedOn);
        }

        [Fact]
        public async Task CreateAsync_ShouldRefuseAnInvalidName()
        {
            var result = await _service.CreateAsync(new RankingGroupRequestDto(" "), Ana, CancellationToken.None);

            Assert.Equal(RankingGroupErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Empty(RankingGroupSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldRefuseTheSixthGroup_CountingJoinedOnes()
        {
            for (var i = 0; i < 4; i++)
                await CreateAsync(Ana, $"Grupo {i}");
            await JoinAsync(await CreateAsync(Bia), Ana);

            var result = await _service.CreateAsync(new RankingGroupRequestDto("Sexto"), Ana, CancellationToken.None);

            Assert.Equal(RankingGroupErrors.TooManyGroups, result.Error);
        }

        [Fact]
        public async Task GetAllAsync_ShouldListOnlyTheUsersGroups_ByName()
        {
            var trabalho = await CreateAsync(Ana, "Trabalho");
            await CreateAsync(Ana, "amigos");
            await CreateAsync(Bia, "Da Bia");
            await JoinAsync(trabalho, Bia);

            var groups = await _service.GetAllAsync(Ana, CancellationToken.None);

            Assert.Equal(["amigos", "Trabalho"], groups.Select(group => group.Name));
            Assert.Equal(2, groups[1].MemberCount);
            Assert.All(groups, group => Assert.True(group.IsOwner));
        }

        [Fact]
        public async Task GetAsync_ShouldAnswerNotFound_ToSomeoneOutsideTheGroup()
        {
            var group = await CreateAsync(Ana);

            var result = await _service.GetAsync(group.Id, Bia, CancellationToken.None);

            Assert.Equal(RankingGroupErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task JoinAsync_ShouldAcceptTheCodeInAnyCase()
        {
            var group = await CreateAsync(Ana);

            var result = await _service.JoinAsync($" {group.InviteCode.ToLowerInvariant()} ", Bia, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.Members.Count);
            Assert.False(result.Value.IsOwner);
            Assert.True(result.Value.Members[1].IsMe);
        }

        [Fact]
        public async Task JoinAsync_ShouldRefuseAMemberTwice()
        {
            var group = await CreateAsync(Ana);

            Assert.Equal(RankingGroupErrors.AlreadyMember, (await _service.JoinAsync(group.InviteCode, Ana, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task JoinAsync_ShouldRefuseThe21stMember()
        {
            var group = await CreateAsync(Ana);
            for (var id = 2; id <= 20; id++)
                await JoinAsync(group, new UserId(id));

            var result = await _service.JoinAsync(group.InviteCode, new UserId(21), CancellationToken.None);

            Assert.Equal(RankingGroupErrors.GroupFull, result.Error);
        }

        [Theory]
        [InlineData("nope")]
        [InlineData("ABCD2345")]
        public async Task JoinAsync_ShouldRefuseAnUnknownCode(string code)
        {
            await CreateAsync(Ana);

            Assert.Equal(RankingGroupErrors.InviteNotFound, (await _service.JoinAsync(code, Bia, CancellationToken.None)).Error);
            Assert.Equal(RankingGroupErrors.InviteNotFound, (await _service.PreviewAsync(code, Bia, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task PreviewAsync_ShouldShowTheGroupBeforeJoining()
        {
            var group = await CreateAsync(Ana);

            var preview = (await _service.PreviewAsync(group.InviteCode, Bia, CancellationToken.None)).Value!;

            Assert.Equal("Família", preview.Name);
            Assert.Equal("Ana", preview.OwnerName);
            Assert.Equal(1, preview.MemberCount);
            Assert.False(preview.AlreadyMember);
            Assert.True((await _service.PreviewAsync(group.InviteCode, Ana, CancellationToken.None)).Value!.AlreadyMember);
        }

        [Fact]
        public async Task RegenerateInviteCodeAsync_ShouldRetireTheOldCode_AndOnlyByTheOwner()
        {
            var group = await CreateAsync(Ana);
            await JoinAsync(group, Bia);

            Assert.Equal(RankingGroupErrors.NotOwner, (await _service.RegenerateInviteCodeAsync(group.Id, Bia, CancellationToken.None)).Error);

            var renewed = (await _service.RegenerateInviteCodeAsync(group.Id, Ana, CancellationToken.None)).Value!;

            Assert.NotEqual(group.InviteCode, renewed.InviteCode);
            Assert.Equal(RankingGroupErrors.InviteNotFound, (await _service.JoinAsync(group.InviteCode, Caio, CancellationToken.None)).Error);
            Assert.True((await _service.JoinAsync(renewed.InviteCode, Caio, CancellationToken.None)).IsSuccess);
        }

        [Fact]
        public async Task RenameAsync_ShouldOnlyBeDoneByTheOwner()
        {
            var group = await CreateAsync(Ana);
            await JoinAsync(group, Bia);

            Assert.Equal(RankingGroupErrors.NotOwner, (await _service.RenameAsync(group.Id, new RankingGroupRequestDto("X"), Bia, CancellationToken.None)).Error);
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.RenameAsync(group.Id, new RankingGroupRequestDto("X"), Caio, CancellationToken.None)).Error);

            var renamed = await _service.RenameAsync(group.Id, new RankingGroupRequestDto("Casa"), Ana, CancellationToken.None);

            Assert.Equal("Casa", renamed.Value!.Name);
            Assert.Equal("Casa", (await _service.GetAsync(group.Id, Bia, CancellationToken.None)).Value!.Name);
        }

        [Fact]
        public async Task LeaveAsync_ShouldPassTheGroupToTheLongestStandingMember_WhenTheOwnerLeaves()
        {
            var group = await CreateAsync(Ana);
            await JoinAsync(group, Bia);
            await JoinAsync(group, Caio);

            Assert.True((await _service.LeaveAsync(group.Id, Ana, CancellationToken.None)).IsSuccess);

            var after = (await _service.GetAsync(group.Id, Bia, CancellationToken.None)).Value!;
            Assert.True(after.IsOwner);
            Assert.Equal(["Bia", "Caio"], after.Members.Select(member => member.Name));
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.GetAsync(group.Id, Ana, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task LeaveAsync_ShouldDeleteTheGroup_WhenTheLastMemberLeaves()
        {
            var group = await CreateAsync(Ana);

            Assert.True((await _service.LeaveAsync(group.Id, Ana, CancellationToken.None)).IsSuccess);

            Assert.Empty(RankingGroupSingleton.Instance);
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.LeaveAsync(group.Id, Ana, CancellationToken.None)).Error);
        }

        [Fact]
        public async Task RemoveMemberAsync_ShouldFollowTheOwnerRules()
        {
            var group = await CreateAsync(Ana);
            var joined = await JoinAsync(group, Bia);
            var ana = joined.Members.Single(member => member.Name == "Ana");
            var bia = joined.Members.Single(member => member.Name == "Bia");

            Assert.Equal(RankingGroupErrors.NotOwner, (await _service.RemoveMemberAsync(group.Id, ana.MemberId, Bia, CancellationToken.None)).Error);
            Assert.Equal(RankingGroupErrors.CannotRemoveSelf, (await _service.RemoveMemberAsync(group.Id, ana.MemberId, Ana, CancellationToken.None)).Error);
            Assert.Equal(RankingGroupErrors.MemberNotFound, (await _service.RemoveMemberAsync(group.Id, 999, Ana, CancellationToken.None)).Error);

            Assert.True((await _service.RemoveMemberAsync(group.Id, bia.MemberId, Ana, CancellationToken.None)).IsSuccess);
            Assert.Equal(RankingGroupErrors.NotFound, (await _service.GetAsync(group.Id, Bia, CancellationToken.None)).Error);

            // A removed member can come back with the current code.
            Assert.True((await _service.JoinAsync(group.InviteCode, Bia, CancellationToken.None)).IsSuccess);
        }
    }
}
