using Millionnika.Api.Auth;
using Xunit;

namespace Millionnika.Api.Tests.Auth;

public sealed class AuthInputValidatorTests
{
    [Fact]
    public void Registration_AcceptsValidInput()
    {
        var request = new RegisterRequest("Игрок_2026", "player@example.com", "StrongPass2026");

        var errors = AuthInputValidator.ValidateRegistration(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void Registration_RejectsInvalidUsernameEmailAndWeakPassword()
    {
        var request = new RegisterRequest("a!", "not-an-email", "weak");

        var errors = AuthInputValidator.ValidateRegistration(request);

        Assert.Contains("username", errors.Keys);
        Assert.Contains("email", errors.Keys);
        Assert.Contains("password", errors.Keys);
        Assert.True(errors["password"].Length >= 3);
    }

    [Fact]
    public void Login_RequiresIdentifierAndPassword()
    {
        var request = new LoginRequest("", "");

        var errors = AuthInputValidator.ValidateLogin(request);

        Assert.Contains("login", errors.Keys);
        Assert.Contains("password", errors.Keys);
    }
}
