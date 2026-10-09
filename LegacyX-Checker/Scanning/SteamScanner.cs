using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// What Steam's own files say: which accounts signed in on this PC and when, when each last played CS2 and for how long,
/// the CS2 launch options, and whether CS2 is installed. The server then asks Steam about the bans on those accounts.
/// </summary>
public static class SteamScanner
{
    private const long SteamIdBase = 76561197960265728;
    private static readonly Regex UserBlock = new("\"(\\d{17})\"\\s*\\{(?<body>[^{}]*)\\}", RegexOptions.Compiled);
    private static readonly Regex Pair = new("\"(?<key>\\w+)\"\\s+\"(?<value>[^\"]*)\"", RegexOptions.Compiled);

    // Launch options that matter to a check. Both have honest uses; both are also how cheats get in.
    private static readonly (string Option, string Note)[] RiskyOptions =
    {
        ("-insecure", "Starts CS2 without VAC. Used for testing, and also to run cheats."),
        ("-allow_third_party_software", "Lets other programs load into CS2. Some overlays need it; cheat loaders do too."),
    };

    private static string Iso(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public static void Scan(ScanContext context)
    {
        try
        {
            var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (string.IsNullOrWhiteSpace(steamPath)) { context.Cs2 = new Cs2Info { Installed = false }; return; }
            ReadAccounts(context, steamPath);
            ReadCs2(context, steamPath);
            context.Log($"[ ok ] steam: {context.SteamAccounts.Count} account(s), cs2 {(context.Cs2?.Installed == true ? "installed" : "not installed")}");
        }
        catch
        {
            // No Steam, or it cannot be read: the lists are just empty.
        }
    }

    private static void ReadAccounts(ScanContext context, string steamPath)
    {
        var file = Path.Combine(steamPath, "config", "loginusers.vdf");
        if (!File.Exists(file)) return;
        foreach (Match block in UserBlock.Matches(File.ReadAllText(file)))
        {
            if (context.SteamAccounts.Count >= 10) break;
            var steamId = block.Groups[1].Value;
            var values = Pair.Matches(block.Groups["body"].Value).ToDictionary(match => match.Groups["key"].Value, match => match.Groups["value"].Value, StringComparer.OrdinalIgnoreCase);
            var account = new SteamAccount
            {
                SteamId = steamId,
                AccountName = Masking.Trim(values.GetValueOrDefault("AccountName", ""), 64),
                PersonaName = Masking.Trim(values.GetValueOrDefault("PersonaName", ""), 64),
                MostRecent = values.GetValueOrDefault("MostRecent", "0") == "1",
            };
            if (long.TryParse(values.GetValueOrDefault("Timestamp", ""), out var login) && login > 0) account.LastLogin = Iso(login);
            ReadPlayTime(steamPath, account, context);
            context.SteamAccounts.Add(account);
            context.SteamIds.Add(steamId);
        }
    }

    /// <summary>When this account last played CS2, for how long, and with which launch options (userdata/&lt;id&gt;/config/localconfig.vdf).</summary>
    private static void ReadPlayTime(string steamPath, SteamAccount account, ScanContext context)
    {
        try
        {
            if (!long.TryParse(account.SteamId, out var id)) return;
            var file = Path.Combine(steamPath, "userdata", (id - SteamIdBase).ToString(), "config", "localconfig.vdf");
            if (!File.Exists(file)) return;
            var text = File.ReadAllText(file);
            var app = Regex.Match(text, "\"730\"\\s*\\{");
            if (!app.Success) return;
            var block = Block(text, app.Index + app.Length - 1);
            var values = Pair.Matches(block).GroupBy(match => match.Groups["key"].Value, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Groups["value"].Value, StringComparer.OrdinalIgnoreCase);
            if (long.TryParse(values.GetValueOrDefault("LastPlayed", ""), out var played) && played > 0) account.Cs2LastPlayed = Iso(played);
            if (double.TryParse(values.GetValueOrDefault("Playtime", ""), out var minutes) && minutes > 0) account.Cs2Hours = Math.Round(minutes / 60.0, 1);
            var options = values.GetValueOrDefault("LaunchOptions", "").Trim();
            if (options.Length == 0) return;
            account.LaunchOptions = Masking.Trim(options, 160);
            foreach (var (option, note) in RiskyOptions)
            {
                if (options.Contains(option, StringComparison.OrdinalIgnoreCase))
                {
                    context.Add(new Finding($"CS2 launch option {option}", "steam", Finding.Suspicion, null, $"{note} Account {account.PersonaName ?? account.SteamId}."));
                }
            }
        }
        catch
        {
            // Not readable: skip this account's play time.
        }
    }

    /// <summary>The text between the brace at <paramref name="open"/> and its matching close brace.</summary>
    private static string Block(string text, int open)
    {
        var depth = 0;
        for (var index = open; index < text.Length; index += 1)
        {
            if (text[index] == '{') depth += 1;
            else if (text[index] == '}' && --depth == 0) return text[(open + 1)..index];
        }
        return text[(open + 1)..];
    }

    /// <summary>Is CS2 (app 730) installed in any Steam library, and when was it last updated?</summary>
    private static void ReadCs2(ScanContext context, string steamPath)
    {
        var libraries = new List<string> { steamPath };
        var folders = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(folders))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(folders), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(library, "steamapps", "appmanifest_730.acf");
            if (!File.Exists(manifest)) continue;
            var info = new Cs2Info { Installed = true };
            var updated = Regex.Match(File.ReadAllText(manifest), "\"LastUpdated\"\\s+\"(\\d+)\"");
            if (updated.Success && long.TryParse(updated.Groups[1].Value, out var seconds) && seconds > 0) info.LastUpdated = Iso(seconds);
            context.Cs2 = info;
            return;
        }
        context.Cs2 = new Cs2Info { Installed = false };
        context.Add(new Finding("CS2 is not installed on this PC", "steam", Finding.Suspicion, null, "A check for CS2 cheats was run on a PC without CS2. It may not be the PC the player plays on."));
    }
}
