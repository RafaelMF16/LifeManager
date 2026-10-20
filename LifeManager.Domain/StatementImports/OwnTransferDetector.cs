using System.Text;
using LifeManager.Domain.Shared.Text;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// Recognizes transfers between the user's own accounts ("Pix enviado: Cp :18236120-FULANO DE TAL", "Transferência
    /// recebida pelo Pix - FULANO DE TAL - …"): a line that starts as a transfer and names the account holder. The holder is
    /// the user's own name, every word of it in order. With <see cref="MinWordsForGaps"/> words or more, the bank may write
    /// more names in between ("Fulano Beltrano Tal" finds "FULANO BELTRANO SOUZA TAL"); a shorter name must be there as
    /// is, since "Maria Silva" would otherwise find "Maria Aparecida Silva", someone else. A name of a single word would
    /// match strangers, so then nothing is detected.
    /// </summary>
    public sealed class OwnTransferDetector
    {
        public const int MinNameWords = 2;
        public const int MinWordsForGaps = 3;

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
            var words = NameWords(Words(holderName)).ToArray();
            return new OwnTransferDetector(words.Length >= MinNameWords ? words : []);
        }

        public bool IsOwnTransfer(string? description)
        {
            if (_holderWords.Length == 0)
                return false;

            var words = Words(description);
            if (words.Count == 0 || !TransferWords.Contains(words[0]))
                return false;

            var names = NameWords(words.Skip(1)).ToList();
            return _holderWords.Length >= MinWordsForGaps ? ContainsInOrder(names) : ContainsTogether(names);
        }

        private bool ContainsInOrder(List<string> names)
        {
            var next = 0;
            foreach (var name in names)
            {
                if (name == _holderWords[next] && ++next == _holderWords.Length)
                    return true;
            }

            return false;
        }

        private bool ContainsTogether(List<string> names)
        {
            for (var start = 0; start + _holderWords.Length <= names.Count; start++)
            {
                if (names.Skip(start).Take(_holderWords.Length).SequenceEqual(_holderWords))
                    return true;
            }

            return false;
        }

        /// <summary>The words that can be part of a name: short words ("cp") and particles ("da") are left out on both sides.</summary>
        private static IEnumerable<string> NameWords(IEnumerable<string> words)
            => words.Where(word => word.Length > 2 && !NameParticles.Contains(word));

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
