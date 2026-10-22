using System.Security.Cryptography;
using System.Text;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Identifies a bank line across imports, so importing the same statement (or an overlapping one) again recognizes
    /// what is already there. A bank's line id (OFX <c>FITID</c>) is only unique within its account, so it is hashed
    /// together with the account the frontend read from the file.
    /// </summary>
    public static class ImportExternalId
    {
        public const int Length = 64;

        /// <returns>Null when the file gave no account or no line id (e.g. most CSV files).</returns>
        public static string? From(string? accountKey, string? fitid)
        {
            if (string.IsNullOrWhiteSpace(accountKey) || string.IsNullOrWhiteSpace(fitid))
                return null;

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{accountKey.Trim()}:{fitid.Trim()}"));
            return Convert.ToHexStringLower(hash);
        }
    }
}
