using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Recognizes the bank's own lines for money put into or taken out of an investment ("Aplicacao: CDB…", "Resgate RDB",
    /// "Credito Tesouro Direto"), so a first import files them without any history. Only the start of the line counts,
    /// and the direction decides: money going out is an application, money coming in is a redemption (an application
    /// refunded included, as "Aplicação - Estorno: …").
    /// </summary>
    public static class InvestmentDetector
    {
        private static readonly string[] ApplicationPrefixes =
        [
            "aplicacao", "aplic", "dinheiro guardado", "compra tesouro direto", "debito tesouro direto"
        ];

        private static readonly string[] RedemptionPrefixes =
        [
            "resgate", "dinheiro resgatado", "credito tesouro direto", "venda tesouro direto", "estorno aplicacao"
        ];

        /// <param name="amount">With the statement's sign: negative when money left the account.</param>
        public static InvestmentMovement? Detect(string? description, decimal amount)
        {
            var text = SearchText.Normalize(description);
            if (text.Length == 0)
                return null;

            if (amount < 0)
                return StartsWithAny(text, ApplicationPrefixes) ? InvestmentMovement.Application : null;

            return StartsWithAny(text, RedemptionPrefixes) || StartsWithAny(text, ApplicationPrefixes)
                ? InvestmentMovement.Redemption
                : null;
        }

        /// <summary>A prefix counts only as whole words: "aplic" matches "aplic cdb" but not "aplicativo".</summary>
        private static bool StartsWithAny(string text, string[] prefixes)
            => prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal)
                && (text.Length == prefix.Length || !char.IsLetterOrDigit(text[prefix.Length])));
    }
}
