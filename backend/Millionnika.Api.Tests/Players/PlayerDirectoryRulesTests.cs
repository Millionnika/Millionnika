using Millionnika.Api.Players;
using Xunit;

namespace Millionnika.Api.Tests.Players;

public sealed class PlayerDirectoryRulesTests
{
    [Fact]
    public void PageSize_RejectsValuesAboveTwenty()
    {
        Assert.True(PlayerDirectoryRules.IsPageValid(1, 20));
        Assert.False(PlayerDirectoryRules.IsPageValid(1, 21));
        Assert.False(PlayerDirectoryRules.IsPageValid(0, 20));
    }

    [Fact]
    public void Search_RejectsMoreThanTwentyFourCharacters()
    {
        Assert.True(PlayerDirectoryRules.IsSearchValid(new string('a', 24)));
        Assert.False(PlayerDirectoryRules.IsSearchValid(new string('a', 25)));
    }

    [Fact]
    public void Description_AcceptsHtmlAsPlainTextButRejectsLongValue()
    {
        Assert.True(PlayerDirectoryRules.IsDescriptionValid("<script>alert('x')</script>"));
        Assert.False(PlayerDirectoryRules.IsDescriptionValid(new string('я', 301)));
    }
}
