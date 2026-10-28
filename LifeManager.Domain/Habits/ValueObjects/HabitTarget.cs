using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.ValueObjects
{
    /// <summary>
    /// A daily amount to reach ("2000 ml"): a habit with one is completed by logging progress, not by a single check-in.
    /// Only habits to build take one. Whole numbers only: the user picks a unit that fits ("m" rather than "km").
    /// </summary>
    public class HabitTarget
    {
        public const int MaxValue = 100_000;
        public const int UnitMaxLength = 15;

        public int Value { get; }
        public string Unit { get; }

        /// <summary>The quick-add amount the frontend offers ("+250"); null when the user only types values.</summary>
        public int? Step { get; }

        private HabitTarget(int value, string unit, int? step)
        {
            Value = value;
            Unit = unit;
            Step = step;
        }

        /// <returns>Null (no target) when <paramref name="value"/> is null and nothing else comes with it.</returns>
        public static Result<HabitTarget?> Create(int? value, string? unit, int? step)
        {
            var trimmedUnit = unit?.Trim();

            if (value is null)
            {
                if (!string.IsNullOrEmpty(trimmedUnit) || step is not null)
                    return HabitErrors.InvalidTargetCombination;

                return (HabitTarget?)null;
            }

            if (value is < 1 or > MaxValue)
                return HabitErrors.InvalidTargetValue;

            if (string.IsNullOrEmpty(trimmedUnit))
                return HabitErrors.TargetUnitRequired;

            if (trimmedUnit.Length > UnitMaxLength)
                return HabitErrors.TargetUnitTooLong;

            if (step is not null && (step < 1 || step > value))
                return HabitErrors.InvalidTargetStep;

            return new HabitTarget(value.Value, trimmedUnit, step);
        }

        /// <returns>Null when <paramref name="value"/> is null: a habit without a target.</returns>
        internal static HabitTarget? FromPersistence(int? value, string? unit, int? step)
            => value is null ? null : new HabitTarget(value.Value, unit ?? string.Empty, step);

        public override bool Equals(object? obj)
        {
            if (obj is HabitTarget other)
                return Value == other.Value && Unit == other.Unit && Step == other.Step;

            return false;
        }

        public override int GetHashCode()
            => HashCode.Combine(Value, Unit, Step);
    }
}
