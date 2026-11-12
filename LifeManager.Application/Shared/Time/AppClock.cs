using Microsoft.Extensions.Configuration;

namespace LifeManager.Application.Shared.Time
{
    /// <summary>A business day and an hour of it (0–23), read from the same instant.</summary>
    public readonly record struct BusinessHour(DateOnly Date, int Hour);

    /// <summary>
    /// "Today" for business rules, in the users' time zone rather than the server's (UTC): a salary due on the 5th
    /// must not be posted at 9 p.m. on the 4th. Backed by <see cref="TimeProvider"/> so tests can set the date.
    /// </summary>
    public class AppClock(TimeProvider timeProvider, IConfiguration configuration)
    {
        public const string TimeZoneKey = "businessTimeZone";
        public const string DefaultTimeZone = "America/Sao_Paulo";

        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly TimeZoneInfo _timeZone = FindTimeZone(configuration[TimeZoneKey] ?? DefaultTimeZone);

        public DateOnly Today()
            => DateOf(_timeProvider.GetUtcNow());

        /// <summary>
        /// The business day and its hour (0–23), e.g. for the habit reminders. Both come from one reading of the clock,
        /// so a call right at midnight never pairs yesterday's date with hour 0.
        /// </summary>
        public BusinessHour CurrentHour()
        {
            var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
            return new BusinessHour(DateOnly.FromDateTime(local.DateTime), local.Hour);
        }

        /// <summary>The business day <paramref name="moment"/> fell on (e.g. the day a habit was archived).</summary>
        public DateOnly DateOf(DateTimeOffset moment)
            => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(moment, _timeZone).DateTime);

        private static TimeZoneInfo FindTimeZone(string id)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone))
                return timeZone;

            throw new InvalidOperationException(
                $"Time zone [{id}] not found. Set [{TimeZoneKey}] to a valid IANA id, and make sure the host has time zone data (tzdata).");
        }
    }
}
