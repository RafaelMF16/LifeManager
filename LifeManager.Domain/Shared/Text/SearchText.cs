using System.Globalization;
using System.Text;

namespace LifeManager.Domain.Shared.Text
{
    /// <summary>
    /// Canonical form used both for persisted searchable columns and for incoming search terms,
    /// so a term always matches the stored value regardless of case and accents ("Saúde" → "saude").
    /// </summary>
    public static class SearchText
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
