using System.Text;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.Categorization
{
    /// <summary>
    /// The part of a bank statement line that names who the money went to or came from, without the noise banks add
    /// around it (dates, card numbers, installments, "COMPRA CARTAO", "PIX ENVIADO"…). Two lines of the same merchant
    /// share the same key, which is what category suggestions are learned and looked up by.
    /// </summary>
    /// <example>"COMPRA CARTAO DEB MC 12/03 IFOOD *RESTAURANTE 1234" → "ifood restaurante".</example>
    public static class MerchantKey
    {
        public const int MaxLength = 100;

        /// <summary>How many words are kept: the merchant is almost always in the first ones; the rest is city, branch…</summary>
        public const int MaxWords = 3;

        /// <summary>Payment-method and transfer words banks put before the merchant; stripped only from the start.</summary>
        private static readonly HashSet<string> LeadingNoise =
        [
            "compra", "compras", "cartao", "debito", "credito", "deb", "cred", "dbt", "pag", "pagto", "pgto", "pagamento",
            "pix", "enviado", "enviada", "recebido", "recebida", "transferencia", "transf", "ted", "doc", "tev",
            "boleto", "automatico", "aut", "nacional", "internacional", "nac", "int", "estab", "est", "qrcode", "qr",
            "elo", "visa", "mastercard", "master", "mc", "maestro", "electron", "no", "na", "de", "da", "do", "com", "via",
            "para", "em", "sispag", "envio", "recebimento"
        ];

        /// <summary>Company-type suffixes and country codes, dropped wherever they are.</summary>
        private static readonly HashSet<string> AnywhereNoise = ["ltda", "eireli", "epp", "me", "sa", "br", "bra"];

        public static string From(string? description)
        {
            var words = Words(SearchText.Normalize(description));

            var meaningful = words
                .Where(word => word.Length > 1 && !word.Any(char.IsDigit) && !AnywhereNoise.Contains(word))
                .ToList();

            var start = 0;
            while (start < meaningful.Count && LeadingNoise.Contains(meaningful[start]))
                start++;

            // A line made only of noise ("PIX RECEBIDO") is still a merchant of its own: keep it instead of an empty key.
            var key = start < meaningful.Count ? meaningful.Skip(start) : meaningful;
            var joined = string.Join(' ', key.Take(MaxWords));

            if (joined.Length == 0)
                joined = string.Join(' ', words.Take(MaxWords));

            return joined.Length > MaxLength ? joined[..MaxLength].TrimEnd() : joined;
        }

        /// <summary>Letters and digits only, so "ifood*restaurante" and "uber.trip" split into words.</summary>
        private static List<string> Words(string normalized)
        {
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');

            return [.. builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        }
    }
}
