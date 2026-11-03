namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>A habit checked in by itself because of a finance action, for the frontend's toast.</summary>
    /// <param name="Wallet">Everything the check-in applied, the milestone's coins included.</param>
    /// <param name="MilestoneDays">The biggest streak milestone it passed; null when none.</param>
    public record AutoCheckInDto(int HabitId, string Name, WalletChangeDto Wallet, int? MilestoneDays, int FreezesEarned)
    {
        public static AutoCheckInDto From(string name, HabitCheckInResultDto checkIn)
            => new(checkIn.HabitId, name, checkIn.Wallet, checkIn.MilestoneDays, checkIn.FreezesEarned);
    }
}
