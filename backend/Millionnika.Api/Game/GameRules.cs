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

    public static void IncreaseStat(GameCharacter character, TrainingStat stat, int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        switch (stat)
        {
            case TrainingStat.Strength: character.Strength += amount; break;
            case TrainingStat.Speed: character.Speed += amount; break;
            case TrainingStat.Defense: character.Defense += amount; break;
            case TrainingStat.Dexterity: character.Dexterity += amount; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
    }
}
