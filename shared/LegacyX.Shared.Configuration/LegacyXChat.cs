using System;
using System.Text;

namespace LegacyX.Shared.Configuration;

/// <summary>
/// Canonical presentation for system-generated LEGACY-X chat messages, the same look as legacyx.cc:
/// "LEGACY-" white and "X" crimson, then dim grey sentences with names and numbers in white. Green
/// only for something that worked or is on. Every other chat color becomes white here, so no module
/// can bring its own palette back. Player-authored chat must not be sent through this formatter.
/// </summary>
public static class LegacyXChat
{
    public const string Prefix = "{white}LEGACY-{brand}X {grey}• ";

    // CS2 chat color codes (the same values as CounterStrikeSharp's ChatColors), so this library
    // needs no CounterStrikeSharp reference.
    private const char White = '\x01';
    private const char Green = '\x04';
    private const char Grey = '\x08';
    private const char Silver = '\x0A';
    // Chat has no exact #e11d48; red is the nearest crimson.
    private const char Brand = '\x07';

    // The tags a message may use ({white}, {grey}, {green}), and what every other color tag becomes.
    // {default} means "back to the body color", which is grey. {brand} is for the prefix only.
    private static readonly (string Tag, char Code)[] Tags =
    {
        ("{white}", White), ("{default}", Grey), ("{grey}", Grey), ("{gray}", Grey), ("{silver}", Grey),
        ("{bluegrey}", Grey), ("{green}", Green), ("{brand}", Brand), ("{lightred}", White),
        ("{darkred}", White), ("{red}", White), ("{lightpurple}", White), ("{purple}", White),
        ("{magenta}", White), ("{olive}", White), ("{lime}", White), ("{lightyellow}", White),
        ("{yellow}", White), ("{gold}", White), ("{orange}", White), ("{lightblue}", White),
        ("{blue}", White), ("{darkblue}", White),
    };

    public static string System(string message)
    {
        var body = string.IsNullOrWhiteSpace(message) ? string.Empty : Palette(message.Trim());
        return (Colorize(Prefix) + body).TrimEnd();
    }

    public static string MatchConnect(string address) => System($"Connect with {{white}}connect {address}");

    /// <summary>
    /// Turns {tags} (any case) into chat color codes; PrintToChat sends text as-is, so an unconverted
    /// tag shows up literally. CS2 cannot color a line's first character, hence the leading space.
    /// </summary>
    public static string Colorize(string text)
    {
        foreach (var (tag, code) in Tags) text = text.Replace(tag, code.ToString(), StringComparison.OrdinalIgnoreCase);
        return text.StartsWith(' ') ? text : " " + text;
    }

    /// <summary>
    /// A message body: tags to codes, then any code outside the palette (a module's own ChatColors, or
    /// tags CounterStrikeSharp's translations already turned into codes) to white; silver to grey.
    /// </summary>
    private static string Palette(string body)
    {
        foreach (var (tag, code) in Tags) body = body.Replace(tag, (code == Brand ? White : code).ToString(), StringComparison.OrdinalIgnoreCase);
        var result = new StringBuilder(body.Length);
        foreach (var c in body)
        {
            if (c >= ' ' || c is White or Green or Grey) result.Append(c);
            else if (c == Silver) result.Append(Grey);
            else if (c == '\r') result.Append(' ');
            else result.Append(White);
        }
        return result.ToString();
    }
}
