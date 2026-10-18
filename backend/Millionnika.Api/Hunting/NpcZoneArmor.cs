using System.Text.Json;

namespace Millionnika.Api.Hunting;

public sealed record NpcZoneProtection(decimal Coverage, decimal Protection);

public static class NpcZoneArmor
{
    public static readonly string[] BodyParts =
        ["head", "throat", "heart", "chest", "stomach", "arms", "hands", "groin", "legs", "feet"];

    public static bool IsValid(IReadOnlyDictionary<string, NpcZoneProtection>? zones) =>
        zones is not null && zones.Count == BodyParts.Length &&
        BodyParts.All(zones.ContainsKey) && zones.All(pair =>
            pair.Value is not null && pair.Value.Coverage is >= 0 and <= 100 &&
            pair.Value.Protection is >= 0 and <= 100);

    public static Dictionary<string, NpcZoneProtection> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, NpcZoneProtection>>(json, CombatJson.Options) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string Write(IReadOnlyDictionary<string, NpcZoneProtection> zones) =>
        JsonSerializer.Serialize(zones, CombatJson.Options);

    public static Dictionary<string, NpcZoneProtection> FromLegacy(decimal armor, string? coverageJson)
    {
        Dictionary<string, decimal> coverage;
        try { coverage = JsonSerializer.Deserialize<Dictionary<string, decimal>>(coverageJson ?? "{}", CombatJson.Options) ?? []; }
        catch (JsonException) { coverage = []; }
        return BodyParts.ToDictionary(part => part, part =>
            new NpcZoneProtection(coverage.GetValueOrDefault(part), coverage.ContainsKey(part) ? armor : 0));
    }
}
