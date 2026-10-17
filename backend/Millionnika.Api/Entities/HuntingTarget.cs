namespace Millionnika.Api.Entities;

public sealed class HuntingTarget
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public HuntingLocation Location { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinimumHuntingLevel { get; set; } = 1;
    public int EnergyCost { get; set; }
    public int Health { get; set; }
    public int Strength { get; set; }
    public int Reflexes { get; set; }
    public int Toughness { get; set; }
    public int Agility { get; set; }
    public string WeaponName { get; set; } = string.Empty;
    public decimal WeaponDamage { get; set; }
    public decimal WeaponAccuracy { get; set; }
    public int ClipSize { get; set; }
    public int RateOfFireMin { get; set; }
    public int RateOfFireMax { get; set; }
    public int SpareClips { get; set; }
    public decimal Armor { get; set; }
    public string ArmorCoverage { get; set; } = "{}";
    public long RewardCredits { get; set; }
    public long RewardExperience { get; set; }
    public long RewardHuntingExperience { get; set; }
    public bool IsActive { get; set; }
}
