namespace LifeManager.Domain.FinanceDashboard.Enums
{
    /// <summary>Which earlier period the dashboard compares the chosen one against.</summary>
    public enum DashboardComparison
    {
        /// <summary>The same number of months right before the period (Apr–Sep → Oct–Mar).</summary>
        PreviousPeriod = 1,

        /// <summary>The same months one year earlier (Jan–Oct 2026 → Jan–Oct 2025). Only for periods up to 12 months.</summary>
        SamePeriodLastYear = 2
    }
}
