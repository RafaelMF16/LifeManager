namespace LifeManager.Domain.Users
{
    /// <summary>The account deletion's grace period: until then, signing in again cancels it.</summary>
    public static class AccountDeletion
    {
        public const int GraceDays = 7;

        public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(GraceDays);
    }
}
