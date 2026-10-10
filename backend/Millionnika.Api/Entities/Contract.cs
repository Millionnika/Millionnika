namespace Millionnika.Api.Entities;

public sealed class Contract
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CostNerve { get; set; }
    public decimal SuccessChance { get; set; }
    public long RewardCredits { get; set; }
    public long RewardExperience { get; set; }
    public int CooldownSeconds { get; set; }
    public bool IsActive { get; set; }
}
