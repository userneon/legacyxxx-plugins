using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>Chat lines reach players in the LEGACY-X palette, as written, never with literal {tags}.</summary>
public class ChatTests
{
    private const string Prefix = " \u0001LEGACY-\u000EX \u0008| ";

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
    public void PlayerLinesReadAllOrTeamBarNameAndText()
    {
        Assert.Equal(" \u0008ALL \u0008| \u0001Temuujin\u0008: \u0001gg", LegacyXChat.Player(false, "Temuujin", "gg"));
        Assert.Equal(" \u0004TEAM \u0008| \u0001[OPERATOR I] Bat\u0008: \u0001push b", LegacyXChat.Player(true, "Bat", "push b", "OPERATOR I"));
    }

    [Theory]
    [InlineData(".r", true)]
    [InlineData("!r", true)]
    [InlineData("  !admin", true)]
    [InlineData("/rs", true)]
    [InlineData(".map de_dust2", true)]
    [InlineData(".stay", true)]
    [InlineData("...gg", false)]
    [InlineData("!!!", false)]
    [InlineData(".", false)]
    [InlineData("hello .r", false)]
    [InlineData(".r2d2!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TypedCommandsAreToldApartFromChat(string? text, bool expected)
    {
        Assert.Equal(expected, LegacyXChat.IsCommandLine(text));
    }

    [Fact]
    public void PlayerNamesWearTheirTeamColor()
    {
        Assert.Equal(" \u0008ALL \u0008| \u000BTemuujin\u0008: \u0001gg", LegacyXChat.Player(false, "Temuujin", "gg", teamNum: 3));
        Assert.Equal(" \u0004TEAM \u0008| \u0009Bat\u0008: \u0001push b", LegacyXChat.Player(true, "Bat", "push b", teamNum: 2));
        Assert.Equal(" \u0008ALL \u0008| \u0001Watcher\u0008: \u0001gg", LegacyXChat.Player(false, "Watcher", "gg", teamNum: 1));
        Assert.Equal(" \u0008ALL \u0008| \u000BEvil\u0008: \u0001red", LegacyXChat.Player(false, "\u0007Evil", "\u0007red", teamNum: 3));
    }

    [Fact]
    public void PlayerLinesCannotCarryColorCodes()
    {
        Assert.Equal(" \u0008ALL \u0008| \u0001Evil\u0008: \u0001red", LegacyXChat.Player(false, "\u0007Evil", "\u0007red"));
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

    [Fact]
    public void A_staff_role_keeps_its_color_in_a_system_line_and_other_codes_still_turn_white()
    {
        var line = LegacyXChat.System("{white}" + LegacyXChat.Role("OWNER") + "777{grey} restarted the round.\u0002 " + LegacyXChat.Role("manager") + "A " + LegacyXChat.Role("Admin") + "B");
        Assert.Contains("\u000EOwner\u0001 777", line);
        Assert.Contains("\u000FManager\u0001 A", line);
        Assert.Contains("\u000BAdmin\u0001 B", line);
        Assert.DoesNotContain('\u0002', line);
        Assert.Equal(string.Empty, LegacyXChat.Role("Player"));
        Assert.Equal(string.Empty, LegacyXChat.Role(null));
    }
}
