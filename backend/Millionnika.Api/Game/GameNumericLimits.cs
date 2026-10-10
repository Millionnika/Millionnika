namespace Millionnika.Api.Game;

using Millionnika.Api.Configuration;

public static class GameNumericLimits
{
    // All values sent to the browser stay far below JavaScript's exact integer limit (2^53 - 1).
    public const long JavaScriptMaxSafeInteger = 9_007_199_254_740_991;
    public const long MaxEconomyValue = 1_000_000_000_000;
    public const int MaxStatValue = 1_000_000;

    public static bool AreCharacterDefaultsValid(CharacterDefaultsOptions options) =>
        options.Credits is >= 0 and <= MaxEconomyValue &&
        options.Experience is >= 0 and <= MaxEconomyValue &&
        options.Strength is >= 0 and <= MaxStatValue && options.Speed is >= 0 and <= MaxStatValue &&
        options.Defense is >= 0 and <= MaxStatValue && options.Dexterity is >= 0 and <= MaxStatValue &&
        options.Energy >= 0 && options.Energy <= options.MaxEnergy && options.MaxEnergy > 0 &&
        options.Nerve >= 0 && options.Nerve <= options.MaxNerve && options.MaxNerve > 0 &&
        options.Health >= 0 && options.Health <= options.MaxHealth && options.MaxHealth > 0;

    public static bool IsTrainingValid(TrainingOptions options) =>
        options.EnergyCost > 0 && options.StatGain is > 0 and <= MaxStatValue && options.ExperienceGain > 0;

    public static bool TryAdd(long current, long amount, long maximum, out long result)
    {
        result = current;
        if (current < 0 || amount < 0 || current > maximum || amount > maximum - current)
            return false;

        result = current + amount;
        return true;
    }

    public static bool TryAdd(int current, int amount, int maximum, out int result)
    {
        result = current;
        if (current < 0 || amount < 0 || current > maximum || amount > maximum - current)
            return false;

        result = current + amount;
        return true;
    }
}
