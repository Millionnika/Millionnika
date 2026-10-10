namespace Millionnika.Api.Configuration;

public sealed class TrainingOptions
{
    public const string SectionName = "Training";

    public int EnergyCost { get; init; }
    public int StatGain { get; init; }
    public int ExperienceGain { get; init; }
}
