namespace Millionnika.Api.Entities;

public sealed class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
