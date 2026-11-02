using System.Security.Cryptography;

namespace LifeManager.Domain.RankingGroups.ValueObjects
{
    /// <summary>
    /// The code (and link) that lets someone into a group: 8 characters from an alphabet without look-alikes (no 0/O,
    /// 1/I/L), so it can be read out loud or typed from a screen.
    /// </summary>
    public class InviteCode
    {
        public const int Length = 8;
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public string Value { get; }

        private InviteCode(string value)
        {
            Value = value;
        }

        public static InviteCode Generate()
            => new(string.Create(Length, 0, (chars, _) =>
            {
                for (var i = 0; i < chars.Length; i++)
                    chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }));

        /// <summary>Reads a typed code, ignoring case and surrounding spaces; false for anything that can't be one.</summary>
        public static bool TryParse(string? value, out InviteCode code)
        {
            code = null!;
            var normalized = value?.Trim().ToUpperInvariant();
            if (normalized is null || normalized.Length != Length || normalized.Any(c => !Alphabet.Contains(c)))
                return false;

            code = new InviteCode(normalized);
            return true;
        }

        internal static InviteCode FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
            => obj is InviteCode other && Value == other.Value;

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
