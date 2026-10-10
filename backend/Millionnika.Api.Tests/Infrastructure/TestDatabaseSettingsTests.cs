using Xunit;

namespace Millionnika.Api.Tests.Infrastructure;

public sealed class TestDatabaseSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Host=localhost;Database=millionnika;Username=millionnika_app")]
    [InlineData("Host=localhost;Database=postgres;Username=millionnika_app")]
    [InlineData("Host=localhost;Database=millionnika_tests_production-1;Username=millionnika_app")]
    [InlineData("Host=remote.example;Database=millionnika_tests;Username=millionnika_app")]
    [InlineData("Host=localhost;Database=millionnika_tests;Username=postgres")]
    [InlineData("Host=localhost;Database=millionnika_tests")]
    [InlineData("Host=localhost;Database=millionnika_tests;Database=millionnika;Username=millionnika_app")]
    public void RejectsUnsafeConfiguration(string? value) =>
        Assert.Throws<InvalidOperationException>(() => TestDatabaseSettings.Validate(value));

    [Theory]
    [InlineData("millionnika_tests")]
    [InlineData("millionnika_tests_clean_20261010")]
    public void AcceptsExplicitLocalTestDatabase(string database) =>
        Assert.Contains(database, TestDatabaseSettings.Validate(
            $"Host=localhost;Database={database};Username=millionnika_app"));

    [Fact]
    public void InvalidConfigurationDoesNotExposeInput()
    {
        var marker = Guid.NewGuid().ToString("N");
        var error = Assert.Throws<InvalidOperationException>(() => TestDatabaseSettings.Validate(
            $"UnsupportedSetting={marker}"));
        Assert.DoesNotContain(marker, error.ToString());
    }
}
