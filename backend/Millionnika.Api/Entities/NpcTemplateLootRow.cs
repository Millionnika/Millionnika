namespace Millionnika.Api.Entities;

public sealed class NpcTemplateLootRow
{
    public Guid Id { get; set; }
    public Guid NpcId { get; set; }
    public NpcTemplate Npc { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public decimal ChancePercent { get; set; }
    public int MinimumQuantity { get; set; }
    public int MaximumQuantity { get; set; }
}
