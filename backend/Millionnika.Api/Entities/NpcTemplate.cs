namespace Millionnika.Api.Entities;

// Unassigned NPCs live here. Hunting robots remain in HuntingTarget as the single
// source of their combat values until location assignment is designed.
public sealed class NpcTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? PortraitPath { get; set; }
    public string? FigurePath { get; set; }
    public int? Health { get; set; }
    public int? Strength { get; set; }
    public int? Toughness { get; set; }
    public int? Reflexes { get; set; }
    public int? Agility { get; set; }
    public string? WeaponName { get; set; }
    public decimal? WeaponDamage { get; set; }
    public decimal? WeaponAccuracy { get; set; }
    public decimal? Armor { get; set; }
    public string? ArmorCoverage { get; set; }
    public string? ArmorZones { get; set; }
    public ICollection<NpcTemplateLootRow> LootRows { get; set; } = [];
}
