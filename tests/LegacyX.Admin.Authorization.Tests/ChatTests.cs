using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>Chat lines reach players in the LEGACY-X palette, upper case, never with literal {tags}.</summary>
public class ChatTests
{
    private const string Prefix = " \u0001LEGACY\u0010-X \u0008• ";

    [Fact]
    public void SystemMessagesUseThePrefixUpperCaseAndCodes()
    {
        var line = LegacyXChat.System("{white}Temuujin{grey} kicked · afk");
        Assert.DoesNotContain("{", line);
        Assert.Equal(Prefix + "\u0001TEMUUJIN\u0008 KICKED · AFK", line);
    }

    [Fact]
    public void ColorsOutsideThePaletteBecomeWhiteAndDefaultIsGrey()
    {
        // Tags and codes a module may still carry: lime / red / gold / a raw ChatColors.Blue.
        var line = LegacyXChat.System("{lime}a{default} {red}b{DEFAULT} {gold}c \u000Bd \u000Ae {green}ok {lightred}no");
        Assert.Equal(Prefix + "\u0001A\u0008 \u0001B\u0008 \u0001C \u0001D \u0008E \u0004OK \u000FNO", line);
    }

    [Fact]
    public void BrandColorStaysInThePrefixOnly()
    {
        var line = LegacyXChat.System("{brand}x{orange}y");
        Assert.Equal(Prefix + "\u0001X\u0001Y", line);
    }

    [Fact]
    public void ColorizeIsCaseInsensitiveAndLeavesPlainTextAlone()
    {
        Assert.Equal(" \u0008[T] \u0001", LegacyXChat.Colorize("{Grey}[T] {WHITE}"));
        Assert.Equal(" plain", LegacyXChat.Colorize("plain"));
        Assert.Equal(" {notacolor}", LegacyXChat.Colorize("{notacolor}"));
    }
}
