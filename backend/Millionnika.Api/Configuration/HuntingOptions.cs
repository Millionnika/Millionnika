namespace Millionnika.Api.Configuration;

public sealed class HuntingOptions
{
    public const string SectionName = "Hunting";
    public int MaximumLevel { get; init; } = 20;
    public int MaximumTurns { get; init; } = 25;
    public int FightDurationSeconds { get; init; } = 300;
    public int ActionCooldownSeconds { get; init; } = 1;
    public int MaximumSpareClips { get; init; } = 3;
    public decimal BaseDamageScale { get; init; } = 2;
    public decimal CreditBonusPerExtraLevel { get; init; } = .02m;
    public decimal MaximumCreditBonus { get; init; } = .20m;
    public decimal DefeatHealthFloor { get; init; } = .25m;
    public Dictionary<string, decimal> BodyPartWeights { get; init; } = [];
    public Dictionary<string, decimal> BodyPartDamageMultipliers { get; init; } = [];
    public decimal[] SpeedRatios { get; init; } = [];
    public decimal[] HitChances { get; init; } = [];
    public decimal[] DefenseRatios { get; init; } = [];
    public decimal[] DamageMitigations { get; init; } = [];
}
