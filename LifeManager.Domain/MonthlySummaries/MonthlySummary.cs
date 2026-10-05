using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.MonthlySummaries
{
    public class MonthlySummary
    {
        public MonthlySummaryId? Id { get; private set; }
        public UserId UserId { get; }
        public MonthlySummaryMonth Month { get; }
        public MonthlySummaryYear Year { get; }
        public TotalIncome TotalIncome { get; private set; }
        public TotalExpense TotalExpense { get; private set; }
        public TotalInvestment TotalInvestment { get; private set; }
        public Balance Balance => Balance.Create(TotalIncome, TotalExpense, TotalInvestment);

        /// <summary>
        /// Persisted copy of <see cref="Balance"/>, kept as its own column so listings can filter and sort
        /// by it in the database. Always derived from <see cref="TotalIncome"/>, <see cref="TotalExpense"/>
        /// and <see cref="TotalInvestment"/>.
        /// </summary>
        public decimal BalanceAmount { get; private set; }

        private MonthlySummary(
            UserId userId,
            MonthlySummaryMonth month,
            MonthlySummaryYear year,
            TotalIncome totalIncome,
            TotalExpense totalExpense,
            TotalInvestment totalInvestment)
        {
            UserId = userId;
            Month = month;
            Year = year;
            TotalIncome = totalIncome;
            TotalExpense = totalExpense;
            TotalInvestment = totalInvestment;
            BalanceAmount = Balance.Value;
        }

        /// <summary>Opens a new month for the user, with no income, expenses or investments yet.</summary>
        public static Result<MonthlySummary> Create(int idUser, int month, int year)
        {
            var userId = new UserId(idUser);

            return MonthlySummaryMonth.Create(month)
                .Bind(summaryMonth => MonthlySummaryYear.Create(year)
                    .Map(summaryYear => new MonthlySummary(
                        userId, summaryMonth, summaryYear, TotalIncome.Zero, TotalExpense.Zero, TotalInvestment.Zero)));
        }

        /// <summary>
        /// Rehydrates a stored summary without the creation rules (current year only, zero totals):
        /// stored summaries can belong to past years and already have totals.
        /// </summary>
        internal static MonthlySummary FromPersistence(
            int id,
            int idUser,
            int month,
            int year,
            decimal totalIncome,
            decimal totalExpense,
            decimal totalInvestment)
        {
            var monthlySummary = new MonthlySummary(
                new UserId(idUser),
                MonthlySummaryMonth.FromPersistence(month),
                MonthlySummaryYear.FromPersistence(year),
                TotalIncome.FromPersistence(totalIncome),
                TotalExpense.FromPersistence(totalExpense),
                TotalInvestment.FromPersistence(totalInvestment));
            monthlySummary.AssignId(id);

            return monthlySummary;
        }

        /// <summary>
        /// Replaces the totals with the ones recalculated from the month's transactions, keeping
        /// <see cref="BalanceAmount"/> in sync.
        /// </summary>
        public Result<MonthlySummary> ApplyTotals(decimal totalIncome, decimal totalExpense, decimal totalInvestment)
        {
            return TotalIncome.Create(totalIncome)
                .Bind(income => TotalExpense.Create(totalExpense)
                    .Bind(expense => TotalInvestment.Create(totalInvestment)
                        .Map(investment =>
                        {
                            TotalIncome = income;
                            TotalExpense = expense;
                            TotalInvestment = investment;
                            BalanceAmount = Balance.Value;
                            return this;
                        })));
        }

        public void AssignId(int id)
        {
            Id = new MonthlySummaryId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not MonthlySummary other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
