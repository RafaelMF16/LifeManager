using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.MonthlySummaries
{
    public class MonthlySummary
    {
        public MonthlySummaryId Id { get; }
        public MonthlySummaryMonth Month { get; }
        public MonthlySummaryYear Year { get; }
        public TotalIncome TotalIncome { get; private set; }
        public TotalExpense TotalExpense { get; private set; }
        public UserId UserId { get; }
        public Balance Balance => Balance.Create(TotalIncome, TotalExpense);

        private MonthlySummary(
            MonthlySummaryId id,
            TotalIncome totalIncome,
            TotalExpense totalExpense,
            UserId userId,
            MonthlySummaryMonth monthlySummaryMonth,
            MonthlySummaryYear monthlySummaryYear)
        {
            Id = id;
            TotalIncome = totalIncome;
            TotalExpense = totalExpense;
            UserId = userId;
            Month = monthlySummaryMonth;
            Year = monthlySummaryYear;
        }

        public static MonthlySummary Create(
            int id,
            decimal incomeValue,
            decimal expenseValue,
            int userId,
            int month,
            int year)
        {
            var monthlySummaryId = new MonthlySummaryId(id);
            var totalIncome = TotalIncome.Create(incomeValue);
            var totalExpense = TotalExpense.Create(expenseValue);
            var idUser = new UserId(userId);
            var monthlySummaryMonth = MonthlySummaryMonth.Create(month);
            var monthlySummaryYear = MonthlySummaryYear.Create(year);

            return new MonthlySummary(monthlySummaryId, totalIncome, totalExpense, idUser, monthlySummaryMonth, monthlySummaryYear);
        }
    }
}