namespace Millionnika.Api.Game;

using Millionnika.Api.Entities;

public enum TrainingStat
{
    Strength,
    Speed,
    Defense,
    Dexterity
}

public static class GameRules
{
    public static bool ContractSucceeds(double roll, decimal successChance) =>
        roll < (double)successChance;

    public static bool TryParseTrainingStat(string? value, out TrainingStat stat) =>
        Enum.TryParse(value, ignoreCase: true, out stat) && Enum.IsDefined(stat);

    public static bool HasEnoughResource(int current, int cost) =>
        cost > 0 && current >= cost;

    public static bool TryGetIncreasedStat(
        GameCharacter character, TrainingStat stat, int amount, out int increased)
    {
        var current = stat switch
        {
            TrainingStat.Strength => character.Strength,
            TrainingStat.Speed => character.Speed,
            TrainingStat.Defense => character.Defense,
            TrainingStat.Dexterity => character.Dexterity,
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
            case TrainingStat.Speed: character.Speed = value; break;
            case TrainingStat.Defense: character.Defense = value; break;
            case TrainingStat.Dexterity: character.Dexterity = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }
}
