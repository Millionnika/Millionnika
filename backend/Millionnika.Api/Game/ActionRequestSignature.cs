using System.Globalization;

namespace Millionnika.Api.Game;

public static class ActionRequestSignature
{
    public static string Contract(Guid contractId) => $"contract:v2:{contractId:D}";

    public static string Training(TrainingStat stat) =>
        $"training:v1:{stat.ToString().ToLowerInvariant()}";

    public static string Purchase(Guid itemId, int quantity) =>
        Build("shop_purchase", itemId, quantity);

    public static string UseItem(Guid itemId, int quantity) =>
        Build("item_use", itemId, quantity);

    public static string Equip(Guid instanceId, string slot) => $"item_equip:v1:{instanceId:D}:{slot}";
    public static string Unequip(string slot) => $"item_unequip:v1:{slot}";

    private static string Build(string action, Guid itemId, int quantity) =>
        string.Create(CultureInfo.InvariantCulture, $"{action}:v1:{itemId:D}:{quantity}");
}
