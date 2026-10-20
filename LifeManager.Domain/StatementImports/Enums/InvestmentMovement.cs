namespace LifeManager.Domain.StatementImports.Enums
{
    /// <summary>What an <see cref="InvestmentDetector"/> found in a bank line.</summary>
    public enum InvestmentMovement
    {
        /// <summary>Money put into an investment: it leaves the account but isn't spending.</summary>
        Application = 1,

        /// <summary>Money taken back out of an investment (an application refunded included): it isn't income.</summary>
        Redemption = 2
    }
}
