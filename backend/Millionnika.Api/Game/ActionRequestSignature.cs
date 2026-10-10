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

    private static string Build(string action, Guid itemId, int quantity) =>
        string.Create(CultureInfo.InvariantCulture, $"{action}:v1:{itemId:D}:{quantity}");
}
