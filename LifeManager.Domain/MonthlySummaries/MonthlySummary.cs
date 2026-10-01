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
        public Balance Balance => Balance.Create(TotalIncome, TotalExpense);

        /// <summary>
        /// Persisted copy of <see cref="Balance"/>, kept as its own column so listings can filter and sort
        /// by it in the database. Always derived from <see cref="TotalIncome"/> and <see cref="TotalExpense"/>.
        /// </summary>
        public decimal BalanceAmount { get; private set; }

        private MonthlySummary(
            UserId userId,
            MonthlySummaryMonth month,
            MonthlySummaryYear year,
            TotalIncome totalIncome,
            TotalExpense totalExpense)
        {
            UserId = userId;
            Month = month;
            Year = year;
            TotalIncome = totalIncome;
            TotalExpense = totalExpense;
            BalanceAmount = Balance.Value;
        }

        /// <summary>Opens a new month for the user, with no income or expenses yet.</summary>
        public static Result<MonthlySummary> Create(int idUser, int month, int year)
        {
            var userId = new UserId(idUser);

            return MonthlySummaryMonth.Create(month)
                .Bind(summaryMonth => MonthlySummaryYear.Create(year)
                    .Map(summaryYear => new MonthlySummary(userId, summaryMonth, summaryYear, TotalIncome.Zero, TotalExpense.Zero)));
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
