using LifeManager.Application.Rewards.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Rewards.Services
{
    /// <remarks>
    /// Deleting a reward archives it: its redemptions keep pointing at it, its name is free for a new reward, and it can
    /// be restored as long as no active reward took that name meanwhile.
    /// </remarks>
    public class RewardService(IRewardRepository rewardRepository, TimeProvider timeProvider)
    {
        private readonly IRewardRepository _rewardRepository = rewardRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<RewardResponseDto>> CreateAsync(RewardDto rewardDto, UserId userId, CancellationToken cancellationToken)
        {
            var rewardResult = Reward.Create(userId.Value, rewardDto.Name, rewardDto.Cost, rewardDto.Icon, _timeProvider.GetUtcNow());
            if (!rewardResult.IsSuccess)
                return rewardResult.Error;

            var reward = rewardResult.Value;

            if (await _rewardRepository.ExistsActiveByNameAsync(userId, reward.Name, null, cancellationToken))
                return RewardErrors.NameAlreadyExists;

            await _rewardRepository.AddAsync(reward, cancellationToken);

            return ToResponseDto(reward);
        }

        public async Task<Result<RewardResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var reward = await _rewardRepository.GetByIdAsync(new RewardId(id), userId, cancellationToken);
            if (reward is null)
                return RewardErrors.NotFound;

            return ToResponseDto(reward);
        }

        public async Task<Result<PagedResponseDto<RewardResponseDto>>> GetPagedAsync(RewardListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;
            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > RewardName.MaxLength)
                return new PagedResponseDto<RewardResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var rewards = await _rewardRepository.GetPagedByUserIdAsync(
                userId, pageRequest, query.Status, normalizedSearch, query.SortBy, query.SortDirection, cancellationToken);

            return PagedResponseDto<RewardResponseDto>.From(rewards, ToResponseDto);
        }

        public async Task<Result<RewardResponseDto>> UpdateAsync(int id, RewardDto rewardDto, UserId userId, CancellationToken cancellationToken)
        {
            var reward = await _rewardRepository.GetByIdAsync(new RewardId(id), userId, cancellationToken);
            if (reward is null)
                return RewardErrors.NotFound;

            var currentNormalizedName = reward.NormalizedName;

            var updateResult = reward.Update(rewardDto.Name, rewardDto.Cost, rewardDto.Icon);
            if (!updateResult.IsSuccess)
                return updateResult.Error;

            if (reward.NormalizedName != currentNormalizedName
                && await _rewardRepository.ExistsActiveByNameAsync(userId, reward.Name, reward.Id, cancellationToken))
                return RewardErrors.NameAlreadyExists;

            await _rewardRepository.UpdateAsync(reward, cancellationToken);

            return ToResponseDto(reward);
        }

        public async Task<Result> ArchiveAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var reward = await _rewardRepository.GetByIdAsync(new RewardId(id), userId, cancellationToken);
            if (reward is null)
                return RewardErrors.NotFound;

            var archiveResult = reward.Archive(_timeProvider.GetUtcNow());
            if (!archiveResult.IsSuccess)
                return archiveResult.Error;

            await _rewardRepository.UpdateAsync(reward, cancellationToken);

            return Result.Success();
        }

        public async Task<Result<RewardResponseDto>> RestoreAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var reward = await _rewardRepository.GetByIdAsync(new RewardId(id), userId, cancellationToken);
            if (reward is null)
                return RewardErrors.NotFound;

            var restoreResult = reward.Restore();
            if (!restoreResult.IsSuccess)
                return restoreResult.Error;

            if (await _rewardRepository.ExistsActiveByNameAsync(userId, reward.Name, reward.Id, cancellationToken))
                return RewardErrors.NameAlreadyExists;

            await _rewardRepository.UpdateAsync(reward, cancellationToken);

            return ToResponseDto(reward);
        }

        private static RewardResponseDto ToResponseDto(Reward reward)
            => new(reward.Id!.Value, reward.Name.Value, reward.Cost, reward.Icon, reward.CreatedAt, reward.ArchivedAt);
    }
}
