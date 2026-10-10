using Millionnika.Api.Inventory;
using Xunit;

namespace Millionnika.Api.Tests.Inventory;

public sealed class ShopRulesTests
{
    [Fact]
    public void Purchase_CalculatesServerPrice()
    {
        Assert.True(ShopRules.TryCalculatePrice(25, 3, out var total));
        Assert.Equal(75, total);
        Assert.True(ShopRules.HasEnoughCredits(100, total));
    }

    [Fact]
    public void Purchase_RejectsInsufficientCredits()
    {
        Assert.False(ShopRules.HasEnoughCredits(49, 50));
    }

    [Fact]
    public void Inventory_RejectsMissingQuantity()
    {
        Assert.False(ShopRules.HasEnoughItems(1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Quantity_RejectsInvalidValues(int quantity)
    {
        Assert.False(ShopRules.IsValidQuantity(quantity));
        Assert.False(ShopRules.TryCalculatePrice(20, quantity, out _));
    }

    [Fact]
    public void ItemEffect_RestoresResource()
    {
        Assert.Equal(60, ShopRules.CalculateRestoredValue(50, 100, 10, 1));
    }

    [Fact]
    public void ItemEffect_DoesNotExceedMaximum()
    {
        Assert.Equal(100, ShopRules.CalculateRestoredValue(95, 100, 20, 1));
    }
}
