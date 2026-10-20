using System.Text;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Recognizes transfers between the user's own accounts ("Pix enviado: Cp :18236120-FULANO DE TAL", "Transferência
    /// recebida pelo Pix - FULANO DE TAL - …"): a line that starts as a transfer and names the account holder. The holder is
    /// the user's own name; the bank may write more names than the user did ("Fulano Tal" finds "FULANO BELTRANO TAL"),
    /// but every name the user gave must be there, in order. A name of a single word would match strangers, so then
    /// nothing is detected.
    /// </summary>
    public sealed class OwnTransferDetector
    {
        public const int MinNameWords = 2;

        private static readonly HashSet<string> TransferWords = ["pix", "transferencia", "transf", "ted", "doc", "tev"];

        /// <summary>Linking words of Portuguese names, which banks drop or keep at will.</summary>
        private static readonly HashSet<string> NameParticles = ["de", "da", "do", "das", "dos", "e"];

        private readonly string[] _holderWords;

        private OwnTransferDetector(string[] holderWords)
        {
            _holderWords = holderWords;
        }

        public static OwnTransferDetector For(string? holderName)
        {
            var words = Words(holderName).Where(word => word.Length > 2 && !NameParticles.Contains(word)).ToArray();
            return new OwnTransferDetector(words.Length >= MinNameWords ? words : []);
        }

        public bool IsOwnTransfer(string? description)
        {
            if (_holderWords.Length == 0)
                return false;

            var words = Words(description);
            if (words.Count == 0 || !TransferWords.Contains(words[0]))
                return false;

            var next = 0;
            foreach (var word in words.Skip(1))
            {
                if (word == _holderWords[next] && ++next == _holderWords.Length)
                    return true;
            }

            return false;
        }

        /// <summary>Normalized letters and digits only, so "Cp :18236120-FULANO" splits into "cp", "18236120", "fulano".</summary>
        private static List<string> Words(string? value)
        {
            var normalized = SearchText.Normalize(value);
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');

            return [.. builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)];
        }
    }
}
