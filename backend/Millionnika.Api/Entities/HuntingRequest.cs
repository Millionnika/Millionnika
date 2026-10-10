namespace Millionnika.Api.Entities;

public sealed class HuntingRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
