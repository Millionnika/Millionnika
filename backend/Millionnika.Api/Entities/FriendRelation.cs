namespace Millionnika.Api.Entities;

public sealed class FriendRelation
{
    public Guid Id { get; set; }
    public Guid UserLowId { get; set; }
    public Guid UserHighId { get; set; }
    public Guid SenderId { get; set; }
    public Guid RecipientId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public User UserLow { get; set; } = null!;
    public User UserHigh { get; set; } = null!;
}
