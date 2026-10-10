namespace Millionnika.Api.Admin;

public static class AdminRules
{
    public static bool IsAdmin(string? role) =>
        string.Equals(role, "Admin", StringComparison.Ordinal);

    public static bool IsReasonValid(string? reason) =>
        reason?.Trim().Length is >= 3 and <= 500;

    public static bool AreResourcesValid(
        long credits, int energy, int maxEnergy, int nerve, int maxNerve, int health, int maxHealth) =>
        credits >= 0 &&
        energy >= 0 && energy <= maxEnergy &&
        nerve >= 0 && nerve <= maxNerve &&
        health >= 0 && health <= maxHealth;

    public static bool IsContractValid(UpdateContractRequest request) =>
        request.CostNerve >= 0 && request.SuccessChance is >= 0 and <= 1 &&
        request.RewardCredits >= 0 && request.RewardExperience >= 0 && IsReasonValid(request.Reason);

    public static bool IsItemValid(UpdateItemRequest request) =>
        request.Price >= 0 && request.Description?.Trim().Length is >= 1 and <= 500 &&
        request.EffectResource is "energy" or "nerve" or "health" &&
        request.EffectAmount > 0 && IsReasonValid(request.Reason);
}
