namespace Millionnika.Api.Admin;

public sealed record AdminNpcResponse(
    Guid Id, string Source, string Category, string Name, string Description, bool IsActive,
    string? PortraitPath, string? FigurePath, int? Health, int? Strength, int? Toughness,
    int? Reflexes, int? Agility, string? WeaponName, decimal? WeaponDamage,
    decimal? WeaponAccuracy, decimal? Armor, string? ArmorCoverage);

public sealed record SaveNpcRequest(
    string? Category, string? Name, string? Description, bool IsActive,
    string? PortraitPath, string? FigurePath, int? Health, int? Strength, int? Toughness,
    int? Reflexes, int? Agility, string? WeaponName, decimal? WeaponDamage,
    decimal? WeaponAccuracy, decimal? Armor, string? ArmorCoverage, string? Reason);
