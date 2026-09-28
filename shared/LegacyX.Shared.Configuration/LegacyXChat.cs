using System;

namespace LegacyX.Shared.Configuration;

/// <summary>
/// Canonical presentation for system-generated LEGACY-X chat messages.
/// Player-authored chat must not be sent through this formatter.
/// </summary>
public static class LegacyXChat
{
    public const string Prefix = "{green}LEGACY-X • {default}";

    // CS2 chat color codes, the same values as CounterStrikeSharp's ChatColors. Kept here so this
    // library needs no CounterStrikeSharp reference.
    private static readonly (string Tag, char Code)[] Colors =
    {
        ("{default}", '\x01'), ("{white}", '\x01'), ("{darkred}", '\x02'), ("{lightpurple}", '\x03'),
        ("{green}", '\x04'), ("{olive}", '\x05'), ("{lime}", '\x06'), ("{red}", '\x07'), ("{grey}", '\x08'),
        ("{lightyellow}", '\x09'), ("{yellow}", '\x09'), ("{silver}", '\x0A'), ("{bluegrey}", '\x0A'),
        ("{lightblue}", '\x0B'), ("{blue}", '\x0B'), ("{darkblue}", '\x0C'), ("{purple}", '\x0E'),
        ("{magenta}", '\x0E'), ("{lightred}", '\x0F'), ("{gold}", '\x10'), ("{orange}", '\x10'),
    };

    public static string System(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Colorize(Prefix.TrimEnd());
        return Colorize($"{Prefix}{message.Trim().ToUpperInvariant()}");
    }

    public static string MatchConnect(string address) => System($"IP: CONNECT {address}");

    /// <summary>
    /// Turns {green}-style tags (any case) into chat color codes; PrintToChat sends text as-is, so an
    /// unconverted tag shows up literally. CS2 cannot color a line's first character, hence the leading space.
    /// </summary>
    public static string Colorize(string text)
    {
        foreach (var (tag, code) in Colors) text = text.Replace(tag, code.ToString(), StringComparison.OrdinalIgnoreCase);
        return text.StartsWith(' ') ? text : " " + text;
    }
}
