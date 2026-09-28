using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>Chat lines reach players with real color codes, never literal {tags}.</summary>
public class ChatTests
{
    [Fact]
    public void SystemMessagesCarryColorCodesNotTags()
    {
        var line = LegacyXChat.System("afk: move or you'll be {lime}kicked{default}");
        Assert.DoesNotContain("{", line);
        Assert.StartsWith(" \x04LEGACY-X • \x01", line);
        Assert.EndsWith("\x06KICKED\x01", line);
    }

    [Fact]
    public void ColorizeIsCaseInsensitiveAndLeavesPlainTextAlone()
    {
        Assert.Equal(" \x07[T] \x01", LegacyXChat.Colorize("{Red}[T] {DEFAULT}"));
        Assert.Equal(" plain", LegacyXChat.Colorize("plain"));
        Assert.Equal(" {notacolor}", LegacyXChat.Colorize("{notacolor}"));
    }
}
