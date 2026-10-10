namespace Millionnika.Api.Admin;

using Millionnika.Api.Game;
using Millionnika.Api.Entities;
using Millionnika.Api.Inventory;

public static class AdminRules
{
    public static bool IsAdmin(string? role) =>
        string.Equals(role, "Admin", StringComparison.Ordinal);

    public static bool IsReasonValid(string? reason) =>
        reason?.Trim().Length is >= 3 and <= 500;

    public static bool AreResourcesValid(
        long credits, int energy, int maxEnergy, int nerve, int maxNerve, int health, int maxHealth) =>
        credits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        energy >= 0 && energy <= maxEnergy &&
        nerve >= 0 && nerve <= maxNerve &&
        health >= 0 && health <= maxHealth;

    public static bool IsContractValid(UpdateContractRequest request) =>
        GameRules.IsValidContractCost(request.CostNerve) && request.SuccessChance is >= 0 and <= 1 &&
        request.RewardCredits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        request.RewardExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        IsProgressionValid(request.Direction, request.MinimumLevel, request.MinimumHackingLevel,
            request.RewardHackingExperience) &&
        (request.Name is null || IsContractTextValid(request.Name, 120)) &&
        (request.Description is null || IsContractTextValid(request.Description, 500)) &&
        IsReasonValid(request.Reason);

    public static bool IsContractValid(CreateContractRequest request) =>
        IsContractTextValid(request.Name, 120) && IsContractTextValid(request.Description, 500) &&
        GameRules.IsValidContractCost(request.CostNerve) && request.SuccessChance is >= 0 and <= 1 &&
        request.RewardCredits is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        request.RewardExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        IsProgressionValid(request.Direction, request.MinimumLevel, request.MinimumHackingLevel,
            request.RewardHackingExperience) && IsReasonValid(request.Reason);

    private static bool IsProgressionValid(string direction, int minimumLevel,
        int minimumHackingLevel, long rewardHackingExperience) =>
        ContractDirections.IsValid(direction) && minimumLevel is >= 1 and <= 20 &&
        minimumHackingLevel is >= 1 and <= 20 &&
        rewardHackingExperience is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        (direction == ContractDirections.Hacking ||
            (minimumHackingLevel == 1 && rewardHackingExperience == 0));

    private static bool IsContractTextValid(string? value, int maximum) =>
        value?.Trim().Length is >= 1 && value.Trim().Length <= maximum;

    public static bool IsItemValid(Item item, UpdateItemRequest request)
    {
        ApplyEditableDefinition(item, request);
        return IsItemValid(item, request.Reason);
    }

    public static bool IsItemValid(Item item, string? reason) =>
        item.Price is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        item.Name.Trim().Length is >= 1 and <= 120 &&
        item.Description.Trim().Length is >= 1 and <= 500 &&
        EquipmentRules.IsDefinitionValid(item) && IsReasonValid(reason);

    public static void ApplyEditableDefinition(Item item, UpdateItemRequest request)
    {
        item.Price = request.Price;
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.EffectResource = request.EffectResource?.Trim() ?? string.Empty;
        item.EffectAmount = request.EffectAmount;
        item.MinimumDamage = request.MinimumDamage; item.MaximumDamage = request.MaximumDamage;
        item.MinimumAccuracy = request.MinimumAccuracy; item.MaximumAccuracy = request.MaximumAccuracy;
        item.MinimumArmor = request.MinimumArmor; item.MaximumArmor = request.MaximumArmor;
        item.ArmorCoverage = request.ArmorCoverage ?? "{}";
        item.IsActive = request.IsActive;
    }

    public static bool IsLegacyConsumableRequest(UpdateItemRequest request) =>
        request.Price is >= 0 and <= GameNumericLimits.MaxEconomyValue &&
        request.Description?.Trim().Length is >= 1 and <= 500 &&
        request.EffectResource is "energy" or "nerve" or "health" &&
        request.EffectAmount > 0 && IsReasonValid(request.Reason);

    public static bool IsItemValid(UpdateItemRequest request) => IsLegacyConsumableRequest(request);
}
