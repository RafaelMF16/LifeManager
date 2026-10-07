namespace LifeManager.Application.Test.Configurations
{
    /// <summary>A clock tests can set. Starts at the real current time, so tests that don't set it behave as before.</summary>
    public class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        /// <summary>
        /// Sets the clock to noon UTC of <paramref name="date"/>, which is the same date in the default business
        /// time zone (America/Sao_Paulo, UTC−3).
        /// </summary>
        public void SetToday(DateOnly date)
            => UtcNow = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    }
}
