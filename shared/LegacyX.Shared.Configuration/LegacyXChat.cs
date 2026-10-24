using System;
using System.Linq;
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
    public const string Prefix = "{white}LEGACY-{brand}X {grey}| ";

    // CS2 chat color codes (the same values as CounterStrikeSharp's ChatColors), so this library
    // needs no CounterStrikeSharp reference.
    private const char White = '\x01';
    private const char Green = '\x04';
    private const char Grey = '\x08';
    private const char Silver = '\x0A';
    // Chat has no exact #8b5cf6; Purple (\x0E) is the nearest violet.
    private const char Brand = '\x0E';

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

    // The name wears its team's chat color (owner request 2026-10-23): CT blue, T yellow, anyone else white. Same codes as
    // CounterStrikeSharp's ChatColors.Blue and ChatColors.Yellow, whose own notes say "Blue for CT, Yellow for T".
    private const char CounterTerrorist = '\x0B';
    private const char Terrorist = '\x09';

    /// <summary>The chat color of a team's names: CT blue, T yellow, anyone else white.</summary>
    public static char TeamColor(int teamNum) => teamNum switch { 3 => CounterTerrorist, 2 => Terrorist, _ => White };

    /// <summary>
    /// A player's chat line as "ALL | Name: text" or "TEAM | Name: text", the name in the color of the team they are on
    /// (<paramref name="teamNum"/>: 3 CT, 2 T). Control characters are cut from the name and the text, so nobody can
    /// paste a color code into chat.
    /// </summary>
    public static string Player(bool team, string name, string text, string tag = "", int teamNum = 0)
    {
        static string Clean(string value) => new string((value ?? string.Empty).Where(c => c >= ' ' && c != '\u007F').ToArray()).Trim();
        var who = Clean(tag).Length > 0 ? $"[{Clean(tag)}] {Clean(name)}" : Clean(name);
        var label = team ? "{green}TEAM" : "{grey}ALL";
        var nameColor = TeamColor(teamNum);
        return Colorize($"{label} {{grey}}| {nameColor}{who}{{grey}}: {{white}}{Clean(text)}");
    }

    /// <summary>
    /// True for a typed plugin command: a "!", "/" or "." and a letter, then a word of letters, digits or "_", then the end or a space
    /// (".r", "!admin", "/rs", ".map de_dust2"). "...gg", "!!!" and "." alone are ordinary chat.
    /// </summary>
    public static bool IsCommandLine(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var line = text.TrimStart();
        static bool Letter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        if (line.Length < 2 || line[0] is not ('!' or '/' or '.') || !Letter(line[1])) return false;
        var i = 2;
        while (i < line.Length && i < 40 && (Letter(line[i]) || (line[i] >= '0' && line[i] <= '9') || line[i] == '_')) i++;
        return i == line.Length || line[i] == ' ';
    }

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
