using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Users.Services
{
    /// <summary>The "download my data" of "My account": the whole account as one JSON file (<see cref="IAccountDataExporter"/>).</summary>
    public class AccountExportService(IAccountDataExporter accountDataExporter, AppClock appClock, TimeProvider timeProvider)
    {
        private readonly IAccountDataExporter _accountDataExporter = accountDataExporter;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary><c>lifemanager-yyyy-MM-dd.json</c>, with today in the business time zone.</summary>
        public string FileName()
            => $"lifemanager-{_appClock.Today():yyyy-MM-dd}.json";

        public Task WriteAsync(UserId userId, Stream output, CancellationToken cancellationToken)
            => _accountDataExporter.WriteAsync(userId, output, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
