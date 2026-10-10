using Millionnika.Api.Configuration;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;
using Xunit;

namespace Millionnika.Api.Tests.Resources;

public sealed class ResourceRecoveryCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly ResourceRecoveryOptions Options = new()
    {
        EnergyAmount = 5,
        EnergyIntervalMinutes = 15,
        NerveAmount = 1,
        NerveIntervalMinutes = 5
    };

    [Fact]
    public void PartialInterval_DoesNotRestoreResource()
    {
        var character = CreateCharacter(energy: 50, nerve: 10, elapsed: TimeSpan.FromMinutes(14).Add(TimeSpan.FromSeconds(59)));

        var result = ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(50, character.Energy);
        Assert.Equal(Now.AddSeconds(1), result.NextEnergyRecoveryAtUtc);
    }

    [Fact]
    public void ExactEnergyBoundary_RestoresFive()
    {
        var character = CreateCharacter(energy: 50, nerve: 10, elapsed: TimeSpan.FromMinutes(15));

        var result = ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(55, character.Energy);
        Assert.Equal(Now.AddMinutes(15), result.NextEnergyRecoveryAtUtc);
    }

    [Fact]
    public void ExactNerveBoundary_RestoresOne()
    {
        var character = CreateCharacter(energy: 100, nerve: 5, elapsed: TimeSpan.FromMinutes(5));

        var result = ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(6, character.Nerve);
        Assert.Equal(Now.AddMinutes(5), result.NextNerveRecoveryAtUtc);
    }

    [Fact]
    public void SeveralIntervals_RestoreAllCompletedIntervalsAndKeepRemainder()
    {
        var character = CreateCharacter(energy: 40, nerve: 10, elapsed: TimeSpan.FromMinutes(46));

        var result = ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(55, character.Energy);
        Assert.Equal(Now.AddMinutes(14), result.NextEnergyRecoveryAtUtc);
    }

    [Fact]
    public void LongAbsence_CapsResourcesAtMaximum()
    {
        var character = CreateCharacter(energy: 25, nerve: 1, elapsed: TimeSpan.FromDays(30));

        var result = ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(100, character.Energy);
        Assert.Equal(10, character.Nerve);
        Assert.Null(result.NextEnergyRecoveryAtUtc);
        Assert.Null(result.NextNerveRecoveryAtUtc);
    }

    [Fact]
    public void Health_DoesNotRecover()
    {
        var character = CreateCharacter(energy: 50, nerve: 5, elapsed: TimeSpan.FromDays(1));
        character.Health = 40;
        character.MaxHealth = 100;

        ResourceRecoveryCalculator.Apply(character, Now, Options);

        Assert.Equal(40, character.Health);
    }

    private static GameCharacter CreateCharacter(int energy, int nerve, TimeSpan elapsed) => new()
    {
        Energy = energy,
        MaxEnergy = 100,
        LastEnergyRecoveryAtUtc = Now - elapsed,
        Nerve = nerve,
        MaxNerve = 10,
        LastNerveRecoveryAtUtc = Now - elapsed,
        Health = 100,
        MaxHealth = 100
    };
}
