namespace LifeManager.Application.AccountHolders.DTOs
{
    /// <param name="Names">The holders as the bank writes them, in the user's order. Required when saving (an empty list clears it).</param>
    public record AccountHoldersDto(IReadOnlyList<string?>? Names);
}
