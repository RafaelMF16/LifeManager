namespace LifeManager.Domain.Auth.Enums
{
    /// <summary>The auth e-mails, each with its own cooldown per address. Stored as an integer: don't renumber.</summary>
    public enum EmailKind
    {
        Confirmation = 1,
        AccountExists = 2,
        PasswordReset = 3
    }
}
