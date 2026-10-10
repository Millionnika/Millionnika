namespace Millionnika.Api.Entities;

public sealed class EquipmentInstance
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public GameCharacter Character { get; set; } = null!;
    public Guid ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public decimal Damage { get; set; }
    public decimal Accuracy { get; set; }
    public decimal Armor { get; set; }
    public string? EquippedSlot { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
