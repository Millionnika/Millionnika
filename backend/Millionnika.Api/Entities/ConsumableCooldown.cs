namespace Millionnika.Api.Entities;

public sealed class ConsumableCooldown
{
    public Guid CharacterId { get; set; }
    public string Group { get; set; } = string.Empty;
    public DateTimeOffset AvailableAtUtc { get; set; }
}
