using Microsoft.AspNetCore.Identity;
using Millionnika.Api.Entities;
using Xunit;

namespace Millionnika.Api.Tests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_IsNotPlainText_AndOnlyCorrectPasswordVerifies()
    {
        var user = new User { Id = Guid.NewGuid(), Username = "Tester" };
        var hasher = new PasswordHasher<User>();
        const string password = "StrongPass2026";

        var hash = hasher.HashPassword(user, password);

        Assert.NotEqual(password, hash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, password));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "WrongPass2026"));
    }
}
