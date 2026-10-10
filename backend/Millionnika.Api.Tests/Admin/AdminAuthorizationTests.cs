using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Millionnika.Api.Admin;
using Millionnika.Api.Game;
using Xunit;

namespace Millionnika.Api.Tests.Admin;

public sealed class AdminAuthorizationTests
{
    [Fact]
    public async Task AdminRole_SatisfiesRoleRequirement()
    {
        var requirement = new RolesAuthorizationRequirement(["Admin"]);
        var principal = PrincipalWithRole("Admin");
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await requirement.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.True(AdminRules.IsAdmin(principal.FindFirstValue(ClaimTypes.Role)));
    }

    [Fact]
    public async Task PlayerRole_DoesNotSatisfyRoleRequirement()
    {
        var requirement = new RolesAuthorizationRequirement(["Admin"]);
        var principal = PrincipalWithRole("Player");
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await requirement.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.False(AdminRules.IsAdmin(principal.FindFirstValue(ClaimTypes.Role)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    public void AuditReason_RejectsMissingOrShortValue(string? reason)
    {
        Assert.False(AdminRules.IsReasonValid(reason));
    }

    [Fact]
    public void AdminValues_AcceptBoundaryAndRejectValuesAboveIt()
    {
        Assert.True(AdminRules.AreResourcesValid(GameNumericLimits.MaxEconomyValue, 1, 1, 1, 1, 1, 1));
        Assert.False(AdminRules.AreResourcesValid(GameNumericLimits.MaxEconomyValue + 1, 1, 1, 1, 1, 1, 1));

        Assert.True(AdminRules.IsContractValid(new UpdateContractRequest(
            1, 1, GameNumericLimits.MaxEconomyValue, GameNumericLimits.MaxEconomyValue, true, "Предел")));
        Assert.False(AdminRules.IsContractValid(new UpdateContractRequest(
            1, 1, GameNumericLimits.MaxEconomyValue + 1, 1, true, "Выше предела")));

        Assert.True(AdminRules.IsItemValid(new UpdateItemRequest(
            GameNumericLimits.MaxEconomyValue, "Описание", "energy", 1, true, "Предел")));
        Assert.False(AdminRules.IsItemValid(new UpdateItemRequest(
            GameNumericLimits.MaxEconomyValue + 1, "Описание", "energy", 1, true, "Выше предела")));
    }

    private static ClaimsPrincipal PrincipalWithRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
}
