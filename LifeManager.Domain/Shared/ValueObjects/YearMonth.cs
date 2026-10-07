using System.Globalization;

namespace LifeManager.Domain.Shared.ValueObjects
{
    /// <summary>A calendar month (year + month): the unit dashboard periods, recurrences and budgets are made of.</summary>
    public sealed record YearMonth
    {
        public const int MinYear = 2000;
        public const int MaxYear = 2100;

        public int Year { get; }
        public int Month { get; }

        private YearMonth(int year, int month)
        {
            Year = year;
            Month = month;
        }

        /// <summary>Months counted from year 0, so two months can be compared and subtracted.</summary>
        public int Ordinal => Year * 12 + Month - 1;

        public DateOnly FirstDay => new(Year, Month, 1);
        public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

        public YearMonth AddMonths(int months) => FromOrdinal(Ordinal + months);

        public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

        /// <summary>Parses "yyyy-MM" (e.g. "2026-09"), with a year between <see cref="MinYear"/> and <see cref="MaxYear"/>.</summary>
        public static bool TryParse(string? value, out YearMonth yearMonth)
        {
            yearMonth = null!;

            if (value is null || value.Length != 7 || value[4] != '-')
                return false;

            if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
                || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month))
                return false;

            if (year < MinYear || year > MaxYear || month < 1 || month > 12)
                return false;

            yearMonth = new YearMonth(year, month);
            return true;
        }

        public override string ToString() => $"{Year:D4}-{Month:D2}";

        private static YearMonth FromOrdinal(int ordinal) => new(ordinal / 12, ordinal % 12 + 1);
    }
}
