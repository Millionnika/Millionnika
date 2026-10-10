using Millionnika.Api.Configuration;
using Millionnika.Api.Game;

namespace Millionnika.Api.Tests.Infrastructure;

public static class TestProgression
{
    public static ProgressionOptions Options => new()
    {
        MaximumLevel = 20,
        GeneralLevelThresholds = Enumerable.Range(1, 20).Select(level => 100L * level * (level - 1) / 2).ToArray(),
        HackingLevelThresholds = Enumerable.Range(1, 20).Select(level => 50L * level * (level - 1) / 2).ToArray(),
        HackingChanceBonusPerLevel = 0.01m,
        MaximumHackingChanceBonus = 0.10m,
        MaximumHackingSuccessChance = 0.95m
    };

    public static IProgressionService Create() => new ProgressionService(
        Microsoft.Extensions.Options.Options.Create(Options));
}
