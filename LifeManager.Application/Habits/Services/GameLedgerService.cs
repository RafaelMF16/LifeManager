using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>Reads the player's statement: every change to coins, XP and HP, newest first.</summary>
    public class GameLedgerService(IGameLedgerRepository gameLedgerRepository)
    {
        private readonly IGameLedgerRepository _gameLedgerRepository = gameLedgerRepository;

        public async Task<Result<PagedResponseDto<GameLedgerEntryResponseDto>>> GetPagedAsync(
            GameLedgerListQueryDto query,
            UserId userId,
            CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var habitId = query.HabitId is null ? null : new HabitId(query.HabitId.Value);
            var entries = await _gameLedgerRepository.GetPagedByUserIdAsync(userId, pageRequestResult.Value, habitId, cancellationToken);

            return PagedResponseDto<GameLedgerEntryResponseDto>.From(entries, GameLedgerEntryResponseDto.From);
        }
    }
}
