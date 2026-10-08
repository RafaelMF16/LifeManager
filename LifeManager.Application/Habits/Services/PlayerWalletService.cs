using LifeManager.Application.Habits.DTOs;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// The door every habits feature goes through to read or change the player's coins, XP and HP. The rules live in
    /// <see cref="PlayerProfile.Apply"/>; this records each change in the ledger, under the profile's row lock.
    /// </summary>
    public class PlayerWalletService(IPlayerProfileRepository playerProfileRepository, TimeProvider timeProvider)
    {
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>The stored profile, or a new player's one (not saved: the profile is created on its first change).</summary>
        public async Task<PlayerProfileResponseDto> GetProfileAsync(UserId userId, CancellationToken cancellationToken)
        {
            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? PlayerProfile.CreateDefault(userId);

            return PlayerProfileResponseDto.From(profile);
        }

        /// <param name="occurredOn">The game day the change belongs to (e.g. the check-in's date).</param>
        /// <param name="description">What caused it (a habit or reward name), kept in the ledger as it is now.</param>
        /// <param name="habitId">The habit behind the change, when there is one.</param>
        public async Task<WalletChangeDto> ApplyAsync(
            UserId userId,
            GameLedgerEntryKind kind,
            GameDelta delta,
            DateOnly occurredOn,
            string? description,
            CancellationToken cancellationToken,
            HabitId? habitId = null)
        {
            var createdAt = _timeProvider.GetUtcNow();
            GameOutcome? outcome = null;

            var profile = await _playerProfileRepository.ApplyAsync(userId, lockedProfile =>
            {
                outcome = lockedProfile.Apply(delta);
                return GameLedgerEntry.FromOutcome(userId, kind, occurredOn, createdAt, description, outcome, habitId);
            }, cancellationToken);

            return WalletChangeDto.From(profile, outcome!);
        }
    }
}
