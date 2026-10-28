using LifeManager.Application.GameItems.DTOs;
using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.GameItems.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.GameItems.Services
{
    /// <summary>
    /// Spending coins on the shop's game items. A purchase applies the item at once and is written in the ledger
    /// (<see cref="GameLedgerEntryKind.ItemPurchased"/>, with the item); the limits and the balance are checked under
    /// the profile's lock, by <see cref="GameItemCatalog"/>.
    /// </summary>
    public class GameItemService(
        IGameItemPurchaseRepository gameItemPurchaseRepository,
        IPlayerProfileRepository playerProfileRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IGameItemPurchaseRepository _gameItemPurchaseRepository = gameItemPurchaseRepository;
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<GameItemsResponseDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? PlayerProfile.CreateDefault(userId);

            var items = new List<GameItemDto>();
            foreach (var kind in GameItemCatalog.All)
            {
                var purchasedInPeriod = await _gameItemPurchaseRepository.CountActiveSinceAsync(
                    userId, kind, GameItemCatalog.PeriodStart(kind, today), cancellationToken);
                items.Add(GameItemDto.From(kind, profile, purchasedInPeriod));
            }

            var todayPurchases = await _gameItemPurchaseRepository.GetByDayAsync(userId, today, cancellationToken);

            return new GameItemsResponseDto(items, [.. todayPurchases.Select(purchase => GameItemPurchaseResponseDto.From(purchase, today))]);
        }

        /// <param name="itemName">The item as the route names it (<c>HpPotion</c>, <c>StreakFreeze</c>; any case).</param>
        public async Task<Result<GameItemPurchaseResultDto>> PurchaseAsync(string itemName, UserId userId, CancellationToken cancellationToken)
        {
            if (!GameItemCatalog.TryParse(itemName, out var kind))
                return GameItemErrors.UnknownItem;

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();

            Error? failure = null;
            GameItemPurchase? purchase = null;
            GameOutcome? outcome = null;

            var profile = await _gameItemPurchaseRepository.PurchaseAsync(userId, kind, GameItemCatalog.PeriodStart(kind, today), (lockedProfile, purchasedInPeriod) =>
            {
                var purchaseResult = GameItemCatalog.Purchase(kind, lockedProfile, purchasedInPeriod);
                if (!purchaseResult.IsSuccess)
                {
                    failure = purchaseResult.Error;
                    return null;
                }

                outcome = purchaseResult.Value.Outcome;
                purchase = GameItemPurchase.Create(userId, kind, GameItemCatalog.Price(kind), purchaseResult.Value.HpRestored, today, now);

                return new GameItemPurchaseDecision(
                    purchase,
                    GameLedgerEntry.FromOutcome(userId, GameLedgerEntryKind.ItemPurchased, today, now, null, outcome, itemKind: kind));
            }, cancellationToken);

            if (profile is null)
                return failure!;

            return new GameItemPurchaseResultDto(GameItemPurchaseResponseDto.From(purchase!, today), WalletChangeDto.From(profile, outcome!));
        }
    }
}
