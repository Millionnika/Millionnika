namespace Millionnika.Api.Hunting;

public interface ILootRoller
{
    long NextInclusive(long minimum, long maximum);
}

public sealed class LootRoller : ILootRoller
{
    public long NextInclusive(long minimum, long maximum) => Random.Shared.NextInt64(minimum, checked(maximum + 1));
}
