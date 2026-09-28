using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>Chat lines reach players in the LEGACY-X palette, as written, never with literal {tags}.</summary>
public class ChatTests
{
    private const string Prefix = " \u0001LEGACY-\u0007X \u0008• ";

    [Fact]
    public void SystemMessagesUseThePrefixAndKeepTheText()
    {
        var line = LegacyXChat.System("{white}Temuujin{grey} was kicked for being AFK.");
        Assert.DoesNotContain("{", line);
        Assert.Equal(Prefix + "\u0001Temuujin\u0008 was kicked for being AFK.", line);
    }

    [Fact]
    public void ColorsOutsideThePaletteBecomeWhiteAndDefaultIsGrey()
    {
        // Tags and codes a module may still carry: lime / red / gold / lightred / a raw ChatColors.Blue and Red.
        var line = LegacyXChat.System("{lime}a{default} {red}b{DEFAULT} {gold}c \u000Bd \u000Ae {green}ok {lightred}no \u0007r");
        Assert.Equal(Prefix + "\u0001a\u0008 \u0001b\u0008 \u0001c \u0001d \u0008e \u0004ok \u0001no \u0001r", line);
    }

    [Fact]
    public void BrandColorStaysInThePrefixOnly()
    {
        var line = LegacyXChat.System("{brand}x{orange}y");
        Assert.Equal(Prefix + "\u0001x\u0001y", line);
    }

    [Fact]
    public void ColorizeIsCaseInsensitiveAndLeavesPlainTextAlone()
    {
        Assert.Equal(" \u0008[T] \u0001", LegacyXChat.Colorize("{Grey}[T] {WHITE}"));
        Assert.Equal(" plain", LegacyXChat.Colorize("plain"));
        Assert.Equal(" {notacolor}", LegacyXChat.Colorize("{notacolor}"));
    }
}
