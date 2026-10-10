using System.Text.Json;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Inventory;

public static class ItemTypes
{
    public const string Consumable = "consumable";
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Ammunition = "ammunition";
    public static bool IsValid(string? value) => value is Consumable or Weapon or Armor or Ammunition;
}

public static class EquipmentSlots
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string Melee = "melee";
    public const string Temporary = "temporary";
    public const string Helmet = "helmet";
    public const string Body = "body";
    public const string Pants = "pants";
    public const string Gloves = "gloves";
    public const string Boots = "boots";

    public static readonly string[] All = [Primary, Secondary, Melee, Temporary, Helmet, Body, Pants, Gloves, Boots];
    public static readonly string[] WeaponPermanent = [Primary, Secondary, Melee];
    public static readonly string[] Armor = [Helmet, Body, Pants, Gloves, Boots];

    public static bool IsValid(string? slot) => slot is not null && All.Contains(slot);
    public static string[] Parse(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public static bool Allows(Item item, string slot) => Parse(item.AllowedSlots).Contains(slot, StringComparer.Ordinal);
}

public static class EquipmentRules
{
    public const decimal MaximumWeaponRating = 1000m;
    public const decimal MaximumAccuracy = 100m;
    public const decimal MaximumArmor = 100m;
    public static readonly HashSet<string> CoverageParts = new(StringComparer.Ordinal)
        { "head", "throat", "heart", "chest", "stomach", "arms", "hands", "groin", "legs", "feet" };

    public static bool IsDefinitionValid(Item item) => item.Type switch
    {
        ItemTypes.Consumable => item.AllowedSlots == string.Empty && item.EffectResource is "energy" or "nerve" or "health" &&
            item.EffectAmount > 0 && RatingsAreZero(item) && item.ArmorCoverage == "{}" && NoAmmo(item),
        ItemTypes.Ammunition => item.AllowedSlots == string.Empty && item.EffectAmount == 0 &&
            item.EffectResource == string.Empty && RatingsAreZero(item) && item.ArmorCoverage == "{}" &&
            !string.IsNullOrWhiteSpace(item.Caliber) && item.ClipSize == 0 && item.RateOfFireMin == 0 && item.RateOfFireMax == 0,
        ItemTypes.Weapon => ValidSlots(item.AllowedSlots, EquipmentSlots.WeaponPermanent) &&
            ValidRange(item.MinimumDamage, item.MaximumDamage, MaximumWeaponRating, requirePositive: true) &&
            ValidRange(item.MinimumAccuracy, item.MaximumAccuracy, MaximumAccuracy, requirePositive: false) &&
            item.MinimumArmor == 0 && item.MaximumArmor == 0 && item.EffectAmount == 0 &&
            item.EffectResource == string.Empty && item.ArmorCoverage == "{}" && ValidWeaponAmmo(item),
        ItemTypes.Armor => ValidSlots(item.AllowedSlots, EquipmentSlots.Armor, exactlyOne: true) &&
            ValidRange(item.MinimumArmor, item.MaximumArmor, MaximumArmor, requirePositive: true) &&
            item.MinimumDamage == 0 && item.MaximumDamage == 0 && item.MinimumAccuracy == 0 &&
            item.MaximumAccuracy == 0 && item.EffectAmount == 0 && item.EffectResource == string.Empty &&
            IsCoverageValid(item.ArmorCoverage) && NoAmmo(item),
        _ => false
    };

    public static bool IsCoverageValid(string? json)
    {
        if (json is null) return false;
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);
            return values is { Count: > 0 } && values.All(pair => CoverageParts.Contains(pair.Key) && pair.Value is >= 0 and <= 100);
        }
        catch (JsonException) { return false; }
        catch (ArgumentNullException) { return false; }
    }

    private static bool ValidRange(decimal minimum, decimal maximum, decimal limit, bool requirePositive) =>
        minimum >= (requirePositive ? 0.01m : 0) && minimum <= maximum && maximum <= limit;
    private static bool ValidSlots(string value, string[] allowed, bool exactlyOne = false)
    {
        var slots = EquipmentSlots.Parse(value);
        return slots.Length > 0 && (!exactlyOne || slots.Length == 1) && slots.Distinct().Count() == slots.Length && slots.All(allowed.Contains);
    }
    private static bool RatingsAreZero(Item item) => item.MinimumDamage == 0 && item.MaximumDamage == 0 &&
        item.MinimumAccuracy == 0 && item.MaximumAccuracy == 0 && item.MinimumArmor == 0 && item.MaximumArmor == 0;
    private static bool NoAmmo(Item item) => item.Caliber == string.Empty && item.ClipSize == 0 && item.RateOfFireMin == 0 && item.RateOfFireMax == 0;
    private static bool ValidWeaponAmmo(Item item)
    {
        var firearm = EquipmentSlots.Parse(item.AllowedSlots).Any(slot => slot is EquipmentSlots.Primary or EquipmentSlots.Secondary);
        return firearm
            ? !string.IsNullOrWhiteSpace(item.Caliber) && item.ClipSize > 0 && item.RateOfFireMin > 0 && item.RateOfFireMin <= item.RateOfFireMax && item.RateOfFireMax <= item.ClipSize
            : NoAmmo(item);
    }
}

public interface IEquipmentRoller { decimal Roll(decimal minimum, decimal maximum); }

public sealed class EquipmentRoller : IEquipmentRoller
{
    public decimal Roll(decimal minimum, decimal maximum)
    {
        if (minimum == maximum) return minimum;
        var minimumCents = decimal.ToInt32(minimum * 100);
        var maximumCents = decimal.ToInt32(maximum * 100);
        return Random.Shared.Next(minimumCents, maximumCents + 1) / 100m;
    }
}
