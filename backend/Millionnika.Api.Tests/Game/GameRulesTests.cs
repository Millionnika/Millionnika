using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Configuration;
using Xunit;

namespace Millionnika.Api.Tests.Game;

public sealed class GameRulesTests
{
    [Theory]
    [InlineData(0.00, true)]
    [InlineData(0.79, true)]
    [InlineData(0.80, false)]
    [InlineData(0.99, false)]
    public void ContractSuccess_UsesConfiguredChance(double roll, bool expected)
    {
        Assert.Equal(expected, GameRules.ContractSucceeds(roll, 0.8m));
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(1, 2, false)]
    [InlineData(100, 10, true)]
    [InlineData(100, 0, false)]
    public void ResourceCheck_RequiresPositiveCostAndEnoughResource(int current, int cost, bool expected)
    {
        Assert.Equal(expected, GameRules.HasEnoughResource(current, cost));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ContractCost_MustBePositive(int cost, bool expected)
    {
        Assert.Equal(expected, GameRules.IsValidContractCost(cost));
    }

    [Theory]
    [InlineData("strength", TrainingStat.Strength)]
    [InlineData("SPEED", TrainingStat.Speed)]
    [InlineData("defense", TrainingStat.Defense)]
    [InlineData("Dexterity", TrainingStat.Dexterity)]
    public void TrainingStat_AllowsOnlyKnownStats(string value, TrainingStat expected)
    {
        Assert.True(GameRules.TryParseTrainingStat(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("credits")]
    [InlineData("energy")]
    [InlineData("")]
    [InlineData(null)]
    public void TrainingStat_RejectsValuesThatCouldModifyOtherFields(string? value)
    {
        Assert.False(GameRules.TryParseTrainingStat(value, out _));
    }

    [Fact]
    public void Training_ChangesOnlySelectedStat()
    {
        var character = new GameCharacter
        {
            Strength = 1,
            Speed = 1,
            Defense = 1,
            Dexterity = 1
        };

        Assert.True(GameRules.TryGetIncreasedStat(character, TrainingStat.Defense, 1, out var increased));
        GameRules.SetStat(character, TrainingStat.Defense, increased);

        Assert.Equal(1, character.Strength);
        Assert.Equal(1, character.Speed);
        Assert.Equal(2, character.Defense);
        Assert.Equal(1, character.Dexterity);
    }

    [Fact]
    public void Training_RejectsStatAboveLimitWithoutChangingCharacter()
    {
        var character = new GameCharacter { Strength = GameNumericLimits.MaxStatValue };

        Assert.False(GameRules.TryGetIncreasedStat(character, TrainingStat.Strength, 1, out _));
        Assert.Equal(GameNumericLimits.MaxStatValue, character.Strength);
    }

    [Fact]
    public void EconomyAddition_AcceptsBoundaryAndRejectsOverflow()
    {
        Assert.True(GameNumericLimits.TryAdd(
            GameNumericLimits.MaxEconomyValue - 1, 1, GameNumericLimits.MaxEconomyValue, out var boundary));
        Assert.Equal(GameNumericLimits.MaxEconomyValue, boundary);
        Assert.False(GameNumericLimits.TryAdd(
            GameNumericLimits.MaxEconomyValue, 1, GameNumericLimits.MaxEconomyValue, out _));
    }

    [Fact]
    public void Configuration_RejectsNumericValuesAboveLimits()
    {
        var defaults = new CharacterDefaultsOptions
        {
            Credits = GameNumericLimits.MaxEconomyValue + 1,
            MaxEnergy = 100, Energy = 100, MaxNerve = 10, Nerve = 10,
            MaxHealth = 100, Health = 100, Strength = 1, Speed = 1, Defense = 1, Dexterity = 1
        };
        Assert.False(GameNumericLimits.AreCharacterDefaultsValid(defaults));
        Assert.False(GameNumericLimits.IsTrainingValid(new TrainingOptions
        {
            EnergyCost = 10, StatGain = GameNumericLimits.MaxStatValue + 1, ExperienceGain = 2
        }));
    }
}
