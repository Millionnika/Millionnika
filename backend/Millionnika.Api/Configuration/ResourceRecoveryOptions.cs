namespace Millionnika.Api.Configuration;

public sealed class ResourceRecoveryOptions
{
    public const string SectionName = "ResourceRecovery";

    public int EnergyAmount { get; init; }
    public int EnergyIntervalMinutes { get; init; }
    public int NerveAmount { get; init; }
    public int NerveIntervalMinutes { get; init; }
}
