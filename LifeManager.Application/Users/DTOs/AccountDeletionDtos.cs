namespace LifeManager.Application.Users.DTOs
{
    public record RequestAccountDeletionDto(string Password);

    /// <param name="ScheduledFor">When the account goes, unless the user signs in before.</param>
    public record AccountDeletionResponseDto(DateTimeOffset ScheduledFor);
}
