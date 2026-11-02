using LifeManager.Application.RankingGroups.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.RankingGroups.Services
{
    /// <summary>
    /// Who is in which ranking group. Every read and write is scoped to groups the user is in: anyone else gets
    /// <see cref="RankingGroupErrors.NotFound"/>, so a group's existence never leaks.
    /// </summary>
    public class RankingGroupService(IRankingGroupRepository rankingGroupRepository, AppClock appClock, TimeProvider timeProvider)
    {
        /// <summary>Tries before giving up on a fresh code; with 31^8 codes a single collision is already unlikely.</summary>
        private const int InviteCodeAttempts = 5;

        private readonly IRankingGroupRepository _rankingGroupRepository = rankingGroupRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<IReadOnlyList<RankingGroupListItemDto>> GetAllAsync(UserId userId, CancellationToken cancellationToken)
            => [.. (await _rankingGroupRepository.GetSummariesAsync(userId, cancellationToken)).Select(RankingGroupListItemDto.From)];

        public async Task<Result<RankingGroupDetailsDto>> GetAsync(int groupId, UserId userId, CancellationToken cancellationToken)
        {
            var view = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (view is null)
                return RankingGroupErrors.NotFound;

            return RankingGroupDetailsDto.From(view, userId);
        }

        public async Task<Result<RankingGroupDetailsDto>> CreateAsync(RankingGroupRequestDto request, UserId userId, CancellationToken cancellationToken)
        {
            var group = RankingGroup.Create(request.Name, userId, await NewInviteCodeAsync(cancellationToken), _timeProvider.GetUtcNow(), _appClock.Today());
            if (!group.IsSuccess)
                return group.Error!;

            var created = await _rankingGroupRepository.CreateAsync(group.Value!, _appClock.Today(), cancellationToken);
            if (!created.IsSuccess)
                return created.Error!;

            return await GetAsync(created.Value!.Value, userId, cancellationToken);
        }

        public async Task<Result<RankingGroupDetailsDto>> RenameAsync(int groupId, RankingGroupRequestDto request, UserId userId, CancellationToken cancellationToken)
        {
            var view = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (view is null)
                return RankingGroupErrors.NotFound;

            var renamed = view.Group.Rename(request.Name, userId);
            if (!renamed.IsSuccess)
                return renamed.Error!;

            // Not the owner anymore: they left (or the group went) since the read.
            if (!await _rankingGroupRepository.SaveNameAsync(view.Group, userId, cancellationToken))
                return RankingGroupErrors.NotOwner;

            return RankingGroupDetailsDto.From(view, userId);
        }

        /// <summary>Gives the group a new invite code; the old one stops working at once.</summary>
        public async Task<Result<RankingGroupDetailsDto>> RegenerateInviteCodeAsync(int groupId, UserId userId, CancellationToken cancellationToken)
        {
            var view = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (view is null)
                return RankingGroupErrors.NotFound;

            var changed = view.Group.ChangeInviteCode(await NewInviteCodeAsync(cancellationToken), userId);
            if (!changed.IsSuccess)
                return changed.Error!;

            if (!await _rankingGroupRepository.SaveInviteCodeAsync(view.Group, userId, cancellationToken))
                return RankingGroupErrors.NotOwner;

            return RankingGroupDetailsDto.From(view, userId);
        }

        public async Task<Result> LeaveAsync(int groupId, UserId userId, CancellationToken cancellationToken)
            => await _rankingGroupRepository.LeaveAsync(new RankingGroupId(groupId), userId, cancellationToken)
                ? Result.Success()
                : RankingGroupErrors.NotFound;

        public async Task<Result> RemoveMemberAsync(int groupId, int memberId, UserId userId, CancellationToken cancellationToken)
        {
            var view = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (view is null)
                return RankingGroupErrors.NotFound;

            if (!view.Group.IsOwnedBy(userId))
                return RankingGroupErrors.NotOwner;

            var member = view.Members.SingleOrDefault(candidate => candidate.MemberId.Value == memberId);
            if (member is null)
                return RankingGroupErrors.MemberNotFound;

            if (member.UserId == userId)
                return RankingGroupErrors.CannotRemoveSelf;

            return await _rankingGroupRepository.RemoveMemberAsync(view.Group.Id!, member.MemberId, userId, cancellationToken);
        }

        public async Task<Result<RankingGroupPreviewDto>> PreviewAsync(string? code, UserId userId, CancellationToken cancellationToken)
        {
            if (!InviteCode.TryParse(code, out var inviteCode))
                return RankingGroupErrors.InviteNotFound;

            var preview = await _rankingGroupRepository.GetPreviewAsync(inviteCode, userId, cancellationToken);
            if (preview is null)
                return RankingGroupErrors.InviteNotFound;

            return RankingGroupPreviewDto.From(preview);
        }

        public async Task<Result<RankingGroupDetailsDto>> JoinAsync(string? code, UserId userId, CancellationToken cancellationToken)
        {
            if (!InviteCode.TryParse(code, out var inviteCode))
                return RankingGroupErrors.InviteNotFound;

            var joined = await _rankingGroupRepository.JoinAsync(inviteCode, userId, _timeProvider.GetUtcNow(), _appClock.Today(), cancellationToken);
            if (!joined.IsSuccess)
                return joined.Error!;

            return await GetAsync(joined.Value!.Value, userId, cancellationToken);
        }

        private async Task<InviteCode> NewInviteCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                var code = InviteCode.Generate();
                // The unique index is the last guard; past the attempts a collision becomes a plain 500.
                if (attempt == InviteCodeAttempts || !await _rankingGroupRepository.IsInviteCodeTakenAsync(code, cancellationToken))
                    return code;
            }
        }
    }
}
