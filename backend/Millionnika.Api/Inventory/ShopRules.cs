namespace Millionnika.Api.Inventory;

public static class ShopRules
{
    public const int MaxQuantityPerRequest = 100;

    public static bool IsValidQuantity(int quantity) =>
        quantity is >= 1 and <= MaxQuantityPerRequest;

    public static bool HasEnoughCredits(long credits, long totalPrice) =>
        credits >= 0 && totalPrice >= 0 && credits >= totalPrice;

    public static bool HasEnoughItems(int available, int requested) =>
        available >= 0 && IsValidQuantity(requested) && available >= requested;

    public static bool TryCalculatePrice(long unitPrice, int quantity, out long totalPrice)
    {
        totalPrice = 0;
        if (unitPrice < 0 || !IsValidQuantity(quantity) || unitPrice > long.MaxValue / quantity)
        {
            return false;
        }

        totalPrice = unitPrice * quantity;
        return true;
    }

    public static int CalculateRestoredValue(
        int current,
        int maximum,
        int effectAmount,
        int quantity)
    {
        if (current < 0 || maximum < 0 || current > maximum || effectAmount <= 0 || !IsValidQuantity(quantity))
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var restored = Math.Min((long)maximum, current + (long)effectAmount * quantity);
        return (int)restored;
    }
}
