namespace Millionnika.Api.Entities;

public sealed class PlayerInventoryItem
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public GameCharacter Character { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int Quantity { get; set; }
}
