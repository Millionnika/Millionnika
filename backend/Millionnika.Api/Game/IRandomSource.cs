namespace Millionnika.Api.Game;

public interface IRandomSource
{
    double NextDouble();
}

public sealed class RandomSource : IRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();
}
