using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Millionnika.Api.Admin;
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

    private static ClaimsPrincipal PrincipalWithRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
}
