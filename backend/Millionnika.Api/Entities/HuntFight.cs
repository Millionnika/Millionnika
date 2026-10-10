namespace Millionnika.Api.Entities;

public sealed class HuntFight
{
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public GameCharacter Character { get; set; } = null!;
    public Guid TargetId { get; set; }
    public string Status { get; set; } = "active";
    public int Turn { get; set; }
    public int PlayerHealth { get; set; }
    public int RobotHealth { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string StateJson { get; set; } = "{}";
    public string LogJson { get; set; } = "[]";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastActionAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool FinalizationApplied { get; set; }
}
