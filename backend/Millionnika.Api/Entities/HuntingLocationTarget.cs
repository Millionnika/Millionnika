namespace Millionnika.Api.Entities;

// Membership only: combat values, rewards and loot belong to one HuntingTarget.
public sealed class HuntingLocationTarget
{
    public Guid LocationId { get; set; }
    public HuntingLocation Location { get; set; } = null!;
    public Guid TargetId { get; set; }
    public HuntingTarget Target { get; set; } = null!;
}
