using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Users.Interfaces
{
    /// <summary>
    /// Writes everything the user owns as one JSON document (the data export of "My account"), streamed section by section
    /// so the account is never held in memory. Never writes secrets (password hash, tokens, login attempts), the bank's
    /// line ids, nor anything about other users.
    /// </summary>
    public interface IAccountDataExporter
    {
        /// <summary>The version of the document's shape, written at its top.</summary>
        const int FormatVersion = 1;

        Task WriteAsync(UserId userId, Stream output, DateTimeOffset exportedAt, CancellationToken cancellationToken);
    }
}
