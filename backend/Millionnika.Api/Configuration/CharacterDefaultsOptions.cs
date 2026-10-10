namespace Millionnika.Api.Configuration;

public sealed class CharacterDefaultsOptions
{
    public const string SectionName = "CharacterDefaults";

    public int Level { get; init; }
    public long Credits { get; init; }
    public int Energy { get; init; }
    public int MaxEnergy { get; init; }
    public int Nerve { get; init; }
    public int MaxNerve { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Strength { get; init; }
    public int Speed { get; init; }
    public int Defense { get; init; }
    public int Dexterity { get; init; }
    public long Experience { get; init; }
}
