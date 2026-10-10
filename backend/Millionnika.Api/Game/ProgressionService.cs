using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Game;

public interface IProgressionService
{
    ProgressionState GetGeneral(long experience);
    ProgressionState GetHacking(long experience);
    ProgressionState GetHunting(long experience);
    LevelChange Apply(GameCharacter character);
    decimal GetContractChance(Contract contract, int hackingLevel);
}

public sealed record ProgressionState(
    int Level, long Experience, long CurrentLevelThreshold, long? NextLevelThreshold)
{
    public bool IsMaximum => NextLevelThreshold is null;
}

public sealed record LevelChange(int PreviousLevel, int Level, int PreviousHackingLevel, int HackingLevel);

public sealed class ProgressionService(IOptions<ProgressionOptions> options) : IProgressionService
{
    private readonly ProgressionOptions settings = options.Value;

    public ProgressionState GetGeneral(long experience) => Calculate(experience, settings.GeneralLevelThresholds);
    public ProgressionState GetHacking(long experience) => Calculate(experience, settings.HackingLevelThresholds);
    public ProgressionState GetHunting(long experience) => Calculate(experience, settings.HuntingLevelThresholds);

    public LevelChange Apply(GameCharacter character)
    {
        var oldLevel = character.Level;
        var oldHackingLevel = character.HackingLevel;
        character.Level = GetGeneral(character.Experience).Level;
        character.HackingLevel = GetHacking(character.HackingExperience).Level;
        character.HuntingLevel = GetHunting(character.HuntingExperience).Level;
        return new LevelChange(oldLevel, character.Level, oldHackingLevel, character.HackingLevel);
    }

    public decimal GetContractChance(Contract contract, int hackingLevel)
    {
        if (!ContractDirections.IsHacking(contract.Direction))
            return contract.SuccessChance;

        var extraLevels = Math.Max(0, hackingLevel - contract.MinimumHackingLevel);
        var bonus = Math.Min(extraLevels * settings.HackingChanceBonusPerLevel,
            settings.MaximumHackingChanceBonus);
        return Math.Min(contract.SuccessChance + bonus, settings.MaximumHackingSuccessChance);
    }

    public static bool IsValid(ProgressionOptions value)
    {
        if (value.MaximumLevel != 20 ||
            !ValidThresholds(value.GeneralLevelThresholds, value.MaximumLevel) ||
            !ValidThresholds(value.HackingLevelThresholds, value.MaximumLevel) ||
            !ValidThresholds(value.HuntingLevelThresholds, value.MaximumLevel))
            return false;

        return value.HackingChanceBonusPerLevel > 0 && value.HackingChanceBonusPerLevel <= 1 &&
            value.MaximumHackingChanceBonus >= value.HackingChanceBonusPerLevel &&
            value.MaximumHackingChanceBonus <= 1 &&
            value.MaximumHackingSuccessChance > 0 && value.MaximumHackingSuccessChance <= 1;
    }

    private static bool ValidThresholds(long[] values, int maximumLevel)
    {
        if (values.Length != maximumLevel || values[0] != 0)
            return false;
        for (var index = 1; index < values.Length; index++)
            if (values[index] <= values[index - 1] || values[index] > GameNumericLimits.MaxEconomyValue)
                return false;
        return true;
    }

    private static ProgressionState Calculate(long experience, long[] thresholds)
    {
        var level = 1;
        for (var index = 1; index < thresholds.Length && experience >= thresholds[index]; index++)
            level = index + 1;
        return new ProgressionState(level, experience, thresholds[level - 1],
            level == thresholds.Length ? null : thresholds[level]);
    }
}

public static class ContractDirections
{
    public const string Normal = "normal";
    public const string Hacking = "hacking";

    public static bool IsValid(string? value) => value is Normal or Hacking;
    public static bool IsHacking(string? value) => value == Hacking;
}
