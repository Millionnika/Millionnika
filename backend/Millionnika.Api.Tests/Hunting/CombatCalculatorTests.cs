using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Game;
using Millionnika.Api.Hunting;
using Xunit;

namespace Millionnika.Api.Tests.Hunting;

public sealed class CombatCalculatorTests
{
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
        SpeedRatios = [.25m, .5m, 1, 2, 4], HitChances = [10, 25, 50, 75, 90],
        DefenseRatios = [.25m, .5m, 1, 2, 4], DamageMitigations = [10, 25, 50, 75, 90]
    };

    private sealed class SequenceRandom(params double[] values) : IRandomSource
    {
        private int index;
        public double NextDouble() => values[Math.Min(index++, values.Length - 1)];
    }
}
