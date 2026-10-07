using Microsoft.Extensions.Configuration;

namespace LifeManager.Application.Shared.Time
{
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
            => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);

        private static TimeZoneInfo FindTimeZone(string id)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone))
                return timeZone;

            throw new InvalidOperationException(
                $"Time zone [{id}] not found. Set [{TimeZoneKey}] to a valid IANA id, and make sure the host has time zone data (tzdata).");
        }
    }
}
