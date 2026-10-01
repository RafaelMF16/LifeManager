using System.Reflection;
using System.Runtime.CompilerServices;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.MonthlySummaries.Mocks
{
    /// <summary>
    /// Builds a <see cref="MonthlySummary"/> the way EF rehydrates it from the database: any year and any totals,
    /// without the creation rules (only the current year, zero totals). Lets tests seed past months and balances.
    /// </summary>
    public static class StoredMonthlySummary
    {
        public static MonthlySummary Create(int id, int userId, int month, int year, decimal totalIncome = 0, decimal totalExpense = 0)
        {
            var monthlySummary = (MonthlySummary)RuntimeHelpers.GetUninitializedObject(typeof(MonthlySummary));

            SetBackingField(monthlySummary, nameof(MonthlySummary.Id), new MonthlySummaryId(id));
            SetBackingField(monthlySummary, nameof(MonthlySummary.UserId), new UserId(userId));
            SetBackingField(monthlySummary, nameof(MonthlySummary.Month), FromPersistence<MonthlySummaryMonth>(month));
            SetBackingField(monthlySummary, nameof(MonthlySummary.Year), FromPersistence<MonthlySummaryYear>(year));
            SetBackingField(monthlySummary, nameof(MonthlySummary.TotalIncome), FromPersistence<TotalIncome>(totalIncome));
            SetBackingField(monthlySummary, nameof(MonthlySummary.TotalExpense), FromPersistence<TotalExpense>(totalExpense));
            SetBackingField(monthlySummary, nameof(MonthlySummary.BalanceAmount), totalIncome - totalExpense);

            return monthlySummary;
        }

        public static MonthlySummary Copy(MonthlySummary monthlySummary)
            => Create(
                monthlySummary.Id!.Value,
                monthlySummary.UserId.Value,
                monthlySummary.Month.Value,
                monthlySummary.Year.Value,
                monthlySummary.TotalIncome.Value,
                monthlySummary.TotalExpense.Value);

        private static T FromPersistence<T>(object value)
        {
            var method = typeof(T).GetMethod("FromPersistence", BindingFlags.Static | BindingFlags.NonPublic)!;

            return (T)method.Invoke(null, [value])!;
        }

        private static void SetBackingField(MonthlySummary monthlySummary, string propertyName, object value)
        {
            var field = typeof(MonthlySummary).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(monthlySummary, value);
        }
    }
}
