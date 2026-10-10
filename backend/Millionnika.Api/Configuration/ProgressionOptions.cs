namespace Millionnika.Api.Configuration;

public sealed class ProgressionOptions
{
    public const string SectionName = "Progression";

    public int MaximumLevel { get; init; }
    public long[] GeneralLevelThresholds { get; init; } = [];
    public long[] HackingLevelThresholds { get; init; } = [];
    public decimal HackingChanceBonusPerLevel { get; init; }
    public decimal MaximumHackingChanceBonus { get; init; }
    public decimal MaximumHackingSuccessChance { get; init; }
}
