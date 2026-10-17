using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Game;
using Millionnika.Api.Hunting;
using Xunit;

namespace Millionnika.Api.Tests.Hunting;

public sealed class CombatCalculatorTests
{
    [Theory]
    [InlineData(1, 1, 50)]
    [InlineData(10, 1, 89.07)]
    public void ReflexesAgainstAgility_UsesTornReferenceCurve(int reflexes, int agility, decimal expected)
    {
        var calculator = new CombatCalculator(Options.Create(Settings()), new SequenceRandom(0));

        Assert.Equal(expected, calculator.BaseHitChance(reflexes, agility));
    }

    [Theory]
    [InlineData(1, 1, 50)]
    [InlineData(4, 1, 30)]
    public void StrengthAgainstToughness_UsesTornReferenceCurve(int strength, int toughness, decimal expected)
    {
        var calculator = new CombatCalculator(Options.Create(Settings()), new SequenceRandom(0));

        Assert.Equal(expected, calculator.ToughnessMitigation(strength, toughness));
    }

    [Fact]
    public void EqualStatsAndAccuracy_FiftyProducesHitAndConfiguredBodyDamage()
    {
        var calculator = new CombatCalculator(Options.Create(Settings()), new SequenceRandom(0, 0, 0.2));
        var hit = calculator.Attack(new(100, 10, 10, 10), new(10, 10, 10, 10),
            new("melee", "Тест", 10, 50, "", 0, 0, 0, 0, 0), []);
        Assert.True(hit.Hit);
        Assert.Equal("head", hit.BodyPart);
        Assert.True(hit.Damage > 0);
    }

    [Fact]
    public void OverlappingArmor_UsesMaximumInsteadOfSum()
    {
        var calculator = new CombatCalculator(Options.Create(Settings()), new SequenceRandom(0, 0, 0, 0));
        var armor = new[] { new CombatArmor(20, new() { ["head"] = 100 }), new CombatArmor(30, new() { ["head"] = 100 }) };
        var hit = calculator.Attack(new(100, 10, 10, 10), new(10, 10, 10, 10),
            new("melee", "Тест", 10, 100, "", 0, 0, 0, 0, 0), armor);
        Assert.Equal(30, hit.Armor);
    }

    [Fact]
    public void FirearmConsumesRemainingRoundsBelowMinimumRate()
    {
        var calculator = new CombatCalculator(Options.Create(Settings()), new SequenceRandom(0.9, 0, 0));
        var hit = calculator.Attack(new(10, 10, 10, 10), new(10, 10, 10, 10),
            new("primary", "Тест", 10, 100, "pulse", 30, 3, 5, 2, 0), []);
        Assert.Equal(2, hit.Bullets);
    }

    private static HuntingOptions Settings() => new()
    {
        MaximumLevel = 20,
        MaximumTurns = 25, FightDurationSeconds = 300, ActionCooldownSeconds = 1, MaximumSpareClips = 3,
        BaseDamageScale = 2, CreditBonusPerExtraLevel = .02m, MaximumCreditBonus = .2m, DefeatHealthFloor = .25m,
        BodyPartWeights = new() { ["head"] = 8, ["throat"] = 3, ["heart"] = 4, ["chest"] = 20, ["stomach"] = 15, ["arms"] = 15, ["hands"] = 5, ["groin"] = 5, ["legs"] = 20, ["feet"] = 5 },
        BodyPartDamageMultipliers = new() { ["head"] = 3.5m, ["throat"] = 3.5m, ["heart"] = 3.5m, ["chest"] = 2, ["stomach"] = 2, ["arms"] = 1, ["hands"] = .7m, ["groin"] = 2, ["legs"] = 1, ["feet"] = .7m },
        ReflexAgilityRatios = [.015625m, .05m, .1m, .2m, .5m, 1m, 1.5m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 20m, 40m, 50m, 64m],
        HitChances = [0m, 5.63m, 10.93m, 18.41m, 33.26m, 50m, 60.49m, 66.74m, 74.15m, 78.57m, 81.59m, 83.81m, 85.54m, 86.94m, 88.10m, 89.07m, 94.37m, 98.11m, 99.06m, 100m],
        ToughnessStrengthRatios = [.03125m, .0625m, .125m, .25m, .5m, 1m, 1.5m, 2m, 3m, 4m, 5m, 6m, 7m, 8m, 9m, 10m, 11m, 12m, 13m, 14m],
        DamageMitigations = [0m, 10m, 20m, 30m, 40m, 50m, 57.68m, 63.14m, 70.81m, 76.26m, 80.49m, 83.95m, 86.87m, 89.40m, 91.63m, 93.63m, 95.43m, 97.08m, 98.60m, 100m]
    };

    private sealed class SequenceRandom(params double[] values) : IRandomSource
    {
        private int index;
        public double NextDouble() => values[Math.Min(index++, values.Length - 1)];
    }
}
