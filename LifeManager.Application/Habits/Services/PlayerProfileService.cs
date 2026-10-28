using LifeManager.Application.Habits.DTOs;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// The profile page: the player's appearance. Nothing here moves coins, XP or HP, so it never goes through
    /// <see cref="PlayerWalletService"/> nor writes the ledger.
    /// </summary>
    public class PlayerProfileService(
        IPlayerProfileRepository playerProfileRepository,
        IAchievementRepository achievementRepository)
    {
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly IAchievementRepository _achievementRepository = achievementRepository;

        /// <summary>
        /// Picks the avatar and the title. The title is checked against the stored unlocks without syncing first: the
        /// frontend's <c>GET Profile</c> already synced the ones the player can see.
        /// </summary>
        public async Task<Result<PlayerProfileResponseDto>> UpdateAppearanceAsync(UpdateAppearanceRequestDto request, UserId userId, CancellationToken cancellationToken)
        {
            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? PlayerProfile.CreateDefault(userId);
            var unlocks = await _achievementRepository.GetUnlocksAsync(userId, cancellationToken);

            var change = profile.ChangeAppearance(request.Avatar, request.Title, unlocks.Select(unlock => unlock.Code));
            if (!change.IsSuccess)
                return change.Error!;

            await _playerProfileRepository.UpdateAppearanceAsync(userId, profile.Avatar, profile.Title, cancellationToken);

            return PlayerProfileResponseDto.From(profile);
        }
    }
}
