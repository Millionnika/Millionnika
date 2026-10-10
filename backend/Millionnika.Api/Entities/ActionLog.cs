namespace Millionnika.Api.Entities;

public sealed class ActionLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RequestId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string RequestSignature { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int EnergySpent { get; set; }
    public int NerveSpent { get; set; }
    public long CreditsAwarded { get; set; }
    public long ExperienceAwarded { get; set; }
    public long HackingExperienceAwarded { get; set; }
    public string? StatName { get; set; }
    public int StatGain { get; set; }
    public Guid? ItemId { get; set; }
    public int ItemQuantity { get; set; }
    public long CreditsSpent { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
