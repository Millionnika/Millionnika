using System.Text.Json;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Game;

namespace Millionnika.Api.Hunting;

public sealed record CombatStats(int Strength, int Reflexes, int Toughness, int Agility);
public sealed record CombatArmor(decimal Armor, Dictionary<string, decimal> Coverage);
public sealed record CombatWeapon(string Slot, string Name, decimal Damage, decimal Accuracy,
    string Caliber, int ClipSize, int RateMin, int RateMax, int AmmoInClip, int ReserveAmmo);
public sealed record CombatHit(bool Hit, string BodyPart, int Damage, decimal Armor, int Bullets, string Message);

public interface ICombatCalculator
{
    CombatHit Attack(CombatStats attacker, CombatStats defender, CombatWeapon weapon, IReadOnlyList<CombatArmor> armor);
}

public sealed class CombatCalculator(IOptions<HuntingOptions> options, IRandomSource random) : ICombatCalculator
{
    private readonly HuntingOptions settings = options.Value;

    public CombatHit Attack(CombatStats attacker, CombatStats defender, CombatWeapon weapon, IReadOnlyList<CombatArmor> armor)
    {
        var baseHit = BaseHitChance(attacker.Reflexes, defender.Agility);
        var attenuation = 4m * baseHit * (100m - baseHit) / 10000m;
        var hitChance = Math.Clamp(baseHit + (weapon.Accuracy - 50m) * attenuation, 0m, 100m);
        var bullets = weapon.ClipSize == 0 ? 0 : Math.Min(weapon.AmmoInClip,
            weapon.RateMin + (int)Math.Floor(random.NextDouble() * (weapon.RateMax - weapon.RateMin + 1)));
        if ((decimal)random.NextDouble() * 100m >= hitChance)
            return new(false, string.Empty, 0, 0, bullets, AttackMessage(weapon, bullets, null, 0));

        var bodyPart = PickBodyPart();
        var multiplier = settings.BodyPartDamageMultipliers[bodyPart];
        var raw = settings.BaseDamageScale * (decimal)Math.Sqrt(Math.Max(1, attacker.Strength)) * weapon.Damage / 10m * multiplier;
        var mitigation = ToughnessMitigation(attacker.Strength, defender.Toughness);
        raw *= 1m - mitigation / 100m;
        var appliedArmor = armor.Where(piece => piece.Coverage.TryGetValue(bodyPart, out var coverage) &&
                (decimal)random.NextDouble() * 100m < coverage)
            .Select(piece => piece.Armor).DefaultIfEmpty(0).Max();
        raw *= 1m - appliedArmor / 100m;
        var damage = Math.Max(1, (int)Math.Floor(raw));
        return new(true, bodyPart, damage, appliedArmor, bullets,
            AttackMessage(weapon, bullets, bodyPart, damage));
    }

    private static string AttackMessage(CombatWeapon weapon, int bullets, string? bodyPart, int damage)
    {
        var result = bodyPart is null
            ? "промах"
            : $"попадание в {BodyPartName(bodyPart)}, урон {damage}";
        return weapon.ClipSize > 0
            ? $"{weapon.Name} выпустил {bullets} {RoundWord(bullets)}: {result}."
            : $"{weapon.Name}: {result}.";
    }

    private static string BodyPartName(string bodyPart) => bodyPart switch
    {
        "head" => "голову",
        "throat" => "горло",
        "heart" => "сердце",
        "chest" => "грудь",
        "stomach" => "живот",
        "arms" => "руки",
        "hands" => "кисти",
        "groin" => "пах",
        "legs" => "ноги",
        "feet" => "ступни",
        _ => bodyPart
    };

    private static string RoundWord(int amount)
    {
        var lastTwo = amount % 100;
        if (lastTwo is >= 11 and <= 14) return "зарядов";
        return (amount % 10) switch { 1 => "заряд", 2 or 3 or 4 => "заряда", _ => "зарядов" };
    }

    internal decimal BaseHitChance(int attackerReflexes, int defenderAgility) =>
        Interpolate(Ratio(attackerReflexes, defenderAgility), settings.ReflexAgilityRatios, settings.HitChances);

    internal decimal ToughnessMitigation(int attackerStrength, int defenderToughness) =>
        Interpolate(Ratio(defenderToughness, attackerStrength), settings.ToughnessStrengthRatios, settings.DamageMitigations);

    private string PickBodyPart()
    {
        var roll = (decimal)random.NextDouble() * settings.BodyPartWeights.Values.Sum();
        foreach (var pair in settings.BodyPartWeights)
        {
            roll -= pair.Value;
            if (roll < 0) return pair.Key;
        }
        return settings.BodyPartWeights.Keys.Last();
    }

    private static decimal Ratio(int left, int right) => right <= 0 ? decimal.MaxValue : (decimal)Math.Max(0, left) / right;
    internal static decimal Interpolate(decimal ratio, decimal[] ratios, decimal[] values)
    {
        if (ratio <= ratios[0]) return values[0];
        for (var i = 1; i < ratios.Length; i++)
            if (ratio <= ratios[i])
            {
                var share = (ratio - ratios[i - 1]) / (ratios[i] - ratios[i - 1]);
                return values[i - 1] + share * (values[i] - values[i - 1]);
            }
        return values[^1];
    }

    public static bool IsValid(HuntingOptions value)
    {
        var parts = new[] { "head", "throat", "heart", "chest", "stomach", "arms", "hands", "groin", "legs", "feet" };
        return value.MaximumLevel == 20 &&
            value.MaximumTurns == 25 && value.FightDurationSeconds == 300 && value.ActionCooldownSeconds == 1 &&
            value.MaximumSpareClips is >= 0 and <= 10 && value.BaseDamageScale > 0 &&
            value.CreditBonusPerExtraLevel >= 0 && value.MaximumCreditBonus is >= 0 and <= 1 &&
            value.DefeatHealthFloor is > 0 and <= 1 &&
            parts.All(part => value.BodyPartWeights.ContainsKey(part) && value.BodyPartWeights[part] > 0 &&
                value.BodyPartDamageMultipliers.ContainsKey(part) && value.BodyPartDamageMultipliers[part] > 0) &&
            ValidCurve(value.ReflexAgilityRatios, value.HitChances) &&
            ValidCurve(value.ToughnessStrengthRatios, value.DamageMitigations) &&
            Interpolate(1m, value.ReflexAgilityRatios, value.HitChances) == 50m &&
            Interpolate(10m, value.ReflexAgilityRatios, value.HitChances) == 89.07m &&
            Interpolate(1m, value.ToughnessStrengthRatios, value.DamageMitigations) == 50m &&
            Interpolate(.25m, value.ToughnessStrengthRatios, value.DamageMitigations) == 30m;
    }

    private static bool ValidCurve(decimal[] ratios, decimal[] values) => ratios.Length >= 2 && ratios.Length == values.Length &&
        ratios[0] > 0 && ratios.Zip(ratios.Skip(1)).All(pair => pair.First < pair.Second) &&
        values.All(value => value is >= 0 and <= 100) &&
        values.Zip(values.Skip(1)).All(pair => pair.First <= pair.Second);
}

internal static class CombatJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
