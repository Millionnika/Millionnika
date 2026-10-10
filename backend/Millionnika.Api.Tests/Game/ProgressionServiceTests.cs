using Millionnika.Api.Entities;
using Millionnika.Api.Game;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Game;

public sealed class ProgressionServiceTests
{
    private readonly IProgressionService progression = TestProgression.Create();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(299, 2)]
    [InlineData(300, 3)]
    [InlineData(600, 4)]
    [InlineData(19000, 20)]
    [InlineData(1000000000000, 20)]
    public void GeneralLevel_UsesExactThresholds(long experience, int expected) =>
        Assert.Equal(expected, progression.GetGeneral(experience).Level);

    [Fact]
    public void Apply_CanCrossSeveralLevelsAndDoesNotSpendExperience()
    {
        var character = new GameCharacter { Level = 1, Experience = 601, HackingLevel = 1, HackingExperience = 301 };
        var change = progression.Apply(character);
        Assert.Equal(4, character.Level);
        Assert.Equal(4, character.HackingLevel);
        Assert.Equal(601, character.Experience);
        Assert.Equal(301, character.HackingExperience);
        Assert.Equal(1, change.PreviousLevel);
    }

    [Theory]
    [InlineData(1, 2, 0.75)]
    [InlineData(2, 2, 0.75)]
    [InlineData(3, 2, 0.76)]
    [InlineData(12, 2, 0.85)]
    [InlineData(20, 2, 0.85)]
    public void HackingChance_AddsOnePointPerExtraLevelAndCapsBonus(int level, int required, decimal expected)
    {
        var contract = new Contract { Direction = ContractDirections.Hacking, MinimumHackingLevel = required, SuccessChance = 0.75m };
        Assert.Equal(expected, progression.GetContractChance(contract, level));
    }

    [Fact]
    public void HackingChance_NeverExceedsNinetyFivePercent()
    {
        var contract = new Contract { Direction = ContractDirections.Hacking, MinimumHackingLevel = 1, SuccessChance = 0.94m };
        Assert.Equal(0.95m, progression.GetContractChance(contract, 20));
    }
}
