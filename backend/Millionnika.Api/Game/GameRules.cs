namespace Millionnika.Api.Game;

using Millionnika.Api.Entities;

public enum TrainingStat
{
    Strength,
    Toughness,
    Reflexes,
    Agility
}

public static class GameRules
{
    public static bool ContractSucceeds(double roll, decimal successChance) =>
        roll < (double)successChance;

    public static bool TryParseTrainingStat(string? value, out TrainingStat stat) =>
        Enum.TryParse(value, ignoreCase: true, out stat) && Enum.IsDefined(stat);

    public static bool HasEnoughResource(int current, int cost) =>
        cost > 0 && current >= cost;

    public static bool IsValidContractCost(int cost) => cost > 0;

    public static bool TryGetIncreasedStat(
        GameCharacter character, TrainingStat stat, int amount, out int increased)
    {
        var current = stat switch
        {
            TrainingStat.Strength => character.Strength,
            TrainingStat.Toughness => character.Toughness,
            TrainingStat.Reflexes => character.Reflexes,
            TrainingStat.Agility => character.Agility,
            _ => throw new ArgumentOutOfRangeException(nameof(stat))
        };
        increased = current;
        return amount > 0 && GameNumericLimits.TryAdd(
            current, amount, GameNumericLimits.MaxStatValue, out increased);
    }

    public static void SetStat(GameCharacter character, TrainingStat stat, int value)
    {
        switch (stat)
        {
            case TrainingStat.Strength: character.Strength = value; break;
            case TrainingStat.Toughness: character.Toughness = value; break;
            case TrainingStat.Reflexes: character.Reflexes = value; break;
            case TrainingStat.Agility: character.Agility = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }
}
