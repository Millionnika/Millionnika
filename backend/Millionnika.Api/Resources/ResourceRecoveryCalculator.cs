using Millionnika.Api.Configuration;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Resources;

public static class ResourceRecoveryCalculator
{
    public static RecoveryState Apply(
        GameCharacter character,
        DateTimeOffset utcNow,
        ResourceRecoveryOptions options)
    {
        var energy = ApplyResource(
            character.Energy,
            character.MaxEnergy,
            character.LastEnergyRecoveryAtUtc,
            utcNow,
            options.EnergyAmount,
            TimeSpan.FromMinutes(options.EnergyIntervalMinutes));

        character.Energy = energy.Current;
        character.LastEnergyRecoveryAtUtc = energy.LastRecoveryAtUtc;

        var nerve = ApplyResource(
            character.Nerve,
            character.MaxNerve,
            character.LastNerveRecoveryAtUtc,
            utcNow,
            options.NerveAmount,
            TimeSpan.FromMinutes(options.NerveIntervalMinutes));

        character.Nerve = nerve.Current;
        character.LastNerveRecoveryAtUtc = nerve.LastRecoveryAtUtc;

        return new RecoveryState(energy.NextRecoveryAtUtc, nerve.NextRecoveryAtUtc);
    }

    private static ResourceRecoveryResult ApplyResource(
        int current,
        int maximum,
        DateTimeOffset lastRecoveryAtUtc,
        DateTimeOffset utcNow,
        int amount,
        TimeSpan interval)
    {
        utcNow = utcNow.ToUniversalTime();
        lastRecoveryAtUtc = lastRecoveryAtUtc.ToUniversalTime();

        if (amount <= 0 || interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Параметры восстановления должны быть положительными.");
        }

        if (lastRecoveryAtUtc > utcNow)
        {
            lastRecoveryAtUtc = utcNow;
        }

        if (current >= maximum)
        {
            return new ResourceRecoveryResult(maximum, lastRecoveryAtUtc, null);
        }

        var elapsedTicks = (utcNow - lastRecoveryAtUtc).Ticks;
        var completedIntervals = elapsedTicks / interval.Ticks;
        if (completedIntervals <= 0)
        {
            return new ResourceRecoveryResult(current, lastRecoveryAtUtc, lastRecoveryAtUtc + interval);
        }

        var recovered = Math.Min((long)maximum - current, completedIntervals * amount);
        current += (int)recovered;

        if (current >= maximum)
        {
            return new ResourceRecoveryResult(maximum, utcNow, null);
        }

        lastRecoveryAtUtc = lastRecoveryAtUtc.AddTicks(completedIntervals * interval.Ticks);
        return new ResourceRecoveryResult(current, lastRecoveryAtUtc, lastRecoveryAtUtc + interval);
    }

    private sealed record ResourceRecoveryResult(
        int Current,
        DateTimeOffset LastRecoveryAtUtc,
        DateTimeOffset? NextRecoveryAtUtc);
}

public sealed record RecoveryState(
    DateTimeOffset? NextEnergyRecoveryAtUtc,
    DateTimeOffset? NextNerveRecoveryAtUtc);
