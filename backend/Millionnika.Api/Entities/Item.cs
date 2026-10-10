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
    public ICollection<PlayerInventoryItem> InventoryEntries { get; set; } = [];
}
