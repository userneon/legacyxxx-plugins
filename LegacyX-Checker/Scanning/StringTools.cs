using System.IO;
using System.Text;

namespace LegacyX.Checker.Scanning;

/// <summary>For staff: the readable text two or more samples of the same cheat have in common, to pick marks for rules.json from.</summary>
public static class StringTools
{
    private const int MinLength = 8;

    private static HashSet<string> Strings(string path)
    {
        var data = File.ReadAllBytes(path);
        var found = new HashSet<string>(StringComparer.Ordinal);
        // Plain text (one byte a letter), then the 2-bytes-a-letter text Windows programs also use.
        var run = new StringBuilder();
        foreach (var value in data)
        {
            if (value >= 0x20 && value < 0x7F) run.Append((char)value);
            else Flush(run, found);
        }
        Flush(run, found);
        for (var offset = 0; offset < 2; offset++)
        {
            for (var index = offset; index + 1 < data.Length; index += 2)
            {
                if (data[index + 1] == 0 && data[index] >= 0x20 && data[index] < 0x7F) run.Append((char)data[index]);
                else Flush(run, found);
            }
            Flush(run, found);
        }
        return found;
    }

    private static void Flush(StringBuilder run, HashSet<string> into)
    {
        if (run.Length >= MinLength) into.Add(run.ToString());
        run.Clear();
    }

    // Names every Windows program has: not a mark of anything.
    private static bool Ordinary(string text) =>
        text.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !text.Contains('\\') && !text.Contains('/')
        || text.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) || text.Contains("Windows", StringComparison.OrdinalIgnoreCase)
        || text.Contains("::") || text.StartsWith("__") || text.StartsWith(".") || text.StartsWith("_") || text.Contains("std") && text.Contains("exception", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Runtime", StringComparison.OrdinalIgnoreCase) || text.Contains("mscoree", StringComparison.OrdinalIgnoreCase) || text.Contains("System.", StringComparison.Ordinal);

    public static string Common(IReadOnlyList<string> files)
    {
        HashSet<string>? common = null;
        foreach (var file in files)
        {
            var set = Strings(file);
            if (common is null) common = set; else common.IntersectWith(set);
        }
        var chosen = (common ?? new HashSet<string>()).Where(text => !Ordinary(text)).OrderByDescending(text => text.Length).Take(300).ToList();
        var lines = new List<string>
        {
            $"Text found in all {files.Count} files, longest first (without the usual Windows and runtime names).",
            "Pick a few that only this cheat has (a window title, a config name, a web address) and put them in rules.json under \"families\":",
            "  { \"name\": \"...\", \"strings\": [\"...\", \"...\"], \"minMatches\": 2 }",
            "",
        };
        lines.AddRange(chosen);
        return string.Join("\n", lines);
    }
}
