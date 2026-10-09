using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>Which Steam accounts have signed in on this PC (from Steam's own loginusers.vdf). The server compares them with the player who was asked.</summary>
public static class SteamScanner
{
    private static readonly Regex Id = new("\"(\\d{17})\"\\s*\\{", RegexOptions.Compiled);

    public static void Scan(ScanContext context)
    {
        try
        {
            var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (string.IsNullOrWhiteSpace(steamPath)) return;
            var file = Path.Combine(steamPath, "config", "loginusers.vdf");
            if (!File.Exists(file)) return;
            foreach (Match match in Id.Matches(File.ReadAllText(file)))
            {
                if (context.SteamIds.Count < 10) context.SteamIds.Add(match.Groups[1].Value);
            }
        }
        catch
        {
            // No Steam, or it cannot be read: the list is just empty.
        }
    }
}
