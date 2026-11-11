using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Users.Mocks
{
    /// <summary>
    /// Records what it was asked to write. The JSON itself is checked against the real schema by the integration tests
    /// (<c>AccountDataExporterTests</c>); a mock of it would prove nothing.
    /// </summary>
    public class AccountDataExporterMock : IAccountDataExporter
    {
        public static (UserId UserId, DateTimeOffset ExportedAt)? LastExport { get; set; }

        public async Task WriteAsync(UserId userId, Stream output, DateTimeOffset exportedAt, CancellationToken cancellationToken)
        {
            LastExport = (userId, exportedAt);
            await output.WriteAsync("{}"u8.ToArray(), cancellationToken);
        }
    }
}
