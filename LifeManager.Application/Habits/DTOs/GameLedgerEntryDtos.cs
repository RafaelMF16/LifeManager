using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Habits/Ledger</c>: always newest first.</summary>
    public record GameLedgerListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;

        /// <summary>Only this habit's entries; every entry when null.</summary>
        public int? HabitId { get; init; }
    }

    /// <summary>One line of the player's statement.</summary>
    /// <param name="OccurredOn">The game day it belongs to, <c>yyyy-MM-dd</c>.</param>
    /// <param name="Description">The habit or reward name as it was then; null for level-ups and knockouts.</param>
    public record GameLedgerEntryResponseDto(
        int Id,
        GameLedgerEntryKind Kind,
        DateOnly OccurredOn,
        DateTimeOffset CreatedAt,
        int CoinsDelta,
        int XpDelta,
        int HpDelta,
        string? Description,
        int? HabitId,
        int? RewardId)
    {
        public static GameLedgerEntryResponseDto From(GameLedgerEntry entry)
            => new(
                entry.Id!.Value,
                entry.Kind,
                entry.OccurredOn,
                entry.CreatedAt,
                entry.CoinsDelta,
                entry.XpDelta,
                entry.HpDelta,
                entry.Description,
                entry.HabitId?.Value,
                entry.RewardId?.Value);
    }
}
