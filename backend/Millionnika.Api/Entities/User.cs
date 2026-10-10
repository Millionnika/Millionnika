namespace Millionnika.Api.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Player";
    public bool IsBlocked { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public GameCharacter Character { get; set; } = null!;
    public ICollection<ActionLog> ActionLogs { get; set; } = [];
    public ICollection<AdminAuditLog> AdminAuditLogs { get; set; } = [];
}
