namespace Millionnika.Api.Entities;

public sealed class HuntingLocation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinimumHuntingLevel { get; set; } = 1;
    public bool IsActive { get; set; }
    public ICollection<HuntingTarget> Targets { get; set; } = [];
}
