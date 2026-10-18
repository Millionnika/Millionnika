namespace Millionnika.Api.Entities;

public sealed class HuntingLootRow
{
    public Guid Id { get; set; }
    public Guid TargetId { get; set; }
    public HuntingTarget Target { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public decimal ChancePercent { get; set; }
    public int MinimumQuantity { get; set; }
    public int MaximumQuantity { get; set; }
}
