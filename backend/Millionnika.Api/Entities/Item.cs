namespace Millionnika.Api.Entities;

public sealed class Item
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Price { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string EffectResource { get; set; } = string.Empty;
    public int EffectAmount { get; set; }
    public int CooldownSeconds { get; set; }
    public string CooldownGroup { get; set; } = string.Empty;
    public string AllowedSlots { get; set; } = string.Empty;
    public decimal MinimumDamage { get; set; }
    public decimal MaximumDamage { get; set; }
    public decimal MinimumAccuracy { get; set; }
    public decimal MaximumAccuracy { get; set; }
    public decimal MinimumArmor { get; set; }
    public decimal MaximumArmor { get; set; }
    public string ArmorCoverage { get; set; } = "{}";
    public string Caliber { get; set; } = string.Empty;
    public int ClipSize { get; set; }
    public int RateOfFireMin { get; set; }
    public int RateOfFireMax { get; set; }
    public ICollection<PlayerInventoryItem> InventoryEntries { get; set; } = [];
    public ICollection<EquipmentInstance> EquipmentInstances { get; set; } = [];
}
