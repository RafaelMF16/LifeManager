namespace LifeManager.Application.Auth.Services
{
    /// <summary>
    /// The BCrypt cost of new password hashes. Production uses the default; the tests register a lower one so hashing
    /// doesn't slow the suite down.
    /// </summary>
    public record PasswordHashingOptions(int WorkFactor = PasswordHashingOptions.DefaultWorkFactor)
    {
        public const int DefaultWorkFactor = 12;
    }
}
