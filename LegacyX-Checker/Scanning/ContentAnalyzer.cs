using System.IO;
using System.Text;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// Looks at what a program IS instead of what it is called. A cheat for CS2 has to reach into another program's memory, and
/// it carries the names of the things it reads (cs2.exe, client.dll, the offsets). An unsigned program that does both is worth a
/// person's attention however it is named. Windows' own and signed programs are left alone.
/// </summary>
public static class ContentAnalyzer
{
    private const long MaxDeepBytes = 64L * 1024 * 1024;

    // What a program asks Windows for tells what it is for. One of these alone is ordinary; together they are what a game cheat needs.
    private static readonly string[] MemoryApis = { "readprocessmemory", "writeprocessmemory", "ntreadvirtualmemory", "ntwritevirtualmemory", "zwreadvirtualmemory", "zwwritevirtualmemory", "virtualallocex", "virtualprotectex" };
    private static readonly string[] InjectionApis = { "createremotethread", "ntcreatethreadex", "rtlcreateuserthread", "queueuserapc", "ntqueueapcthread", "ntmapviewofsection", "setwindowshookexa", "setwindowshookexw" };
    private static readonly string[] KernelApis = { "mmcopyvirtualmemory", "kestackattachprocess", "pslookupprocessbyprocessid" };
    private static readonly string[] OverlayApis = { "setlayeredwindowattributes", "updatelayeredwindow", "dwmextendframeintoclientarea", "d3d11createdeviceandswapchain" };
    private static readonly string[] InputApis = { "sendinput", "mouse_event", "keybd_event", "setcursorpos" };
    private static readonly string[] FindProgramApis = { "createtoolhelp32snapshot", "process32first", "process32firstw", "process32next", "process32nextw", "module32first", "module32firstw", "module32next", "module32nextw", "enumprocessmodules", "enumprocessmodulesex" };
    private static readonly string[] AllApis = MemoryApis.Concat(InjectionApis).Concat(KernelApis).Concat(OverlayApis).Concat(InputApis).Concat(FindProgramApis).Concat(new[] { "openprocess" }).ToArray();

    private static readonly string[] Extensions = { ".exe", ".dll", ".sys" };

    public static bool IsCandidate(string path, long length)
    {
        if (length < 16 * 1024 || length > MaxDeepBytes) return false;
        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
        // Windows' own programs are signed and are the largest part of any disk.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return !(windows.Length > 0 && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Everything the content check learned about one file, and why it did or did not raise a finding.</summary>
    public sealed class Inspection
    {
        public string Path { get; init; } = "";
        public long Length { get; init; }
        public bool Candidate { get; init; }
        public bool IsProgram { get; set; }
        public bool Signed { get; set; }
        public bool Managed { get; set; }
        public List<string> Sections { get; } = new();
        public string? Protector { get; set; }
        public int ImportCount { get; set; }
        public bool OpensProcesses { get; set; }
        public List<string> MemoryApis { get; } = new();
        public bool UsesMemory { get; set; }
        public bool ReadText { get; set; }
        public List<string> GameMarkers { get; } = new();
        public List<string> OffsetMarkers { get; } = new();
        /// <summary>What the program does, each with the points it adds (the verdict comes from the total).</summary>
        public List<(string What, int Points)> Signals { get; } = new();
        public int Score => Signals.Sum(signal => signal.Points);
        /// <summary>Its folder or its name looks like a cheat's: only a reason to look a little closer, never a verdict.</summary>
        public bool Hinted { get; set; }
        public Finding? Finding { get; set; }
        /// <summary>In plain words: why this file was or was not flagged.</summary>
        public string Reason { get; set; } = "";
    }

    public static void Analyze(ScanContext context, string path, long length)
    {
        var inspection = Inspect(context.Rules, path, length);
        if (inspection.Finding is not null) context.Add(inspection.Finding);
    }

    /// <summary>Digits only, a long run of hex, or letters and digits jumbled together: not a name a person would give a program.</summary>
    public static bool LooksRandom(string stem)
    {
        if (stem.Length < 6 || stem.Any(character => !char.IsLetterOrDigit(character))) return false;
        var digits = stem.Count(char.IsDigit);
        if (digits == stem.Length) return true;
        if (stem.Length >= 10 && stem.All(Uri.IsHexDigit)) return true;
        var letters = stem.Count(char.IsLetter);
        return stem.Length >= 8 && digits >= 3 && letters >= 3 && digits * 100 / stem.Length >= 30;
    }

    private static bool RunsFromUserFolder(string path)
    {
        string[] roots =
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            System.IO.Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        };
        return roots.Any(root => !string.IsNullOrEmpty(root) && path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static bool Has(HashSet<string> names, string[] group) => group.Any(names.Contains);

    /// <summary>
    /// Looks at one file the way the scan does, and writes down every step. The verdict is what the program DOES (the Windows functions it uses,
    /// whether it is signed, what it names inside) added up as points. Its name never decides: a name only lowers the bar a little.
    /// </summary>
    public static Inspection Inspect(Rules rules, string path, long length)
    {
        var name = Path.GetFileName(path);
        var result = new Inspection { Path = path, Length = length, Candidate = IsCandidate(path, length) };
        if (!result.Candidate)
        {
            result.Reason = length < 16 * 1024 ? "Skipped: the file is smaller than 16 KB." : length > MaxDeepBytes ? "Skipped: the file is larger than 64 MB." : !Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) ? $"Skipped: only .exe, .dll and .sys files are read for content (this is \"{Path.GetExtension(path)}\")." : "Skipped: it is inside the Windows folder.";
            return result;
        }
        var pe = PeFile.Read(path);
        if (pe is null)
        {
            result.Reason = "Not read: the file is not a Windows program (no valid PE header), or it could not be opened.";
            return result;
        }
        result.IsProgram = true;
        result.Signed = pe.IsSigned;
        result.Managed = pe.IsManaged;
        result.Sections.AddRange(pe.SectionNames);
        result.ImportCount = pe.Imports.Count;
        result.Protector = pe.SectionNames.FirstOrDefault(section => rules.ProtectorSections.Contains(section, StringComparer.OrdinalIgnoreCase));
        // A cheat staff have seen: its marks (strings) are inside, whatever the file is called and even when it is signed with a stolen certificate.
        if (rules.Families.Count > 0)
        {
            var marks = rules.Families.SelectMany(family => family.Strings).Distinct().ToList();
            var inside = marks.Count > 0 ? FindWords(path, marks.Append("LegacyX.Checker.Scanning"), ignoreCase: false) : null;
            // The checker itself (an older build) carries the rules and so every mark.
            if (inside is not null && !inside.Contains("LegacyX.Checker.Scanning"))
            {
                foreach (var family in rules.Families)
                {
                    var matched = family.Strings.Where(inside.Contains).ToList();
                    if (family.Strings.Count > 0 && matched.Count >= Math.Max(1, family.MinMatches))
                    {
                        result.Finding = new Finding(name, "file", Finding.Detection, path, $"Matches the {family.Name} cheat ({matched.Count} of its marks)");
                        result.Reason = $"DETECTION: it holds {matched.Count} marks of the {family.Name} cheat: {string.Join(", ", matched.Take(5))}.";
                        return result;
                    }
                }
            }
        }
        if (pe.IsSigned)
        {
            result.Reason = "Not flagged: the program has a digital signature, and signed programs are left alone.";
            return result;
        }

        // Which of the interesting functions it uses: from its import table, or for a .NET program from the names in its own data.
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (pe.IsManaged)
        {
            if (FindWords(path, AllApis, ignoreCase: true) is { } found) names.UnionWith(found);
        }
        else
        {
            names.UnionWith(pe.Imports.Where(import => AllApis.Contains(import, StringComparer.OrdinalIgnoreCase)));
        }
        result.OpensProcesses = names.Contains("openprocess");
        result.MemoryApis.AddRange(MemoryApis.Where(names.Contains));

        if (result.OpensProcesses && Has(names, MemoryApis)) result.Signals.Add(("reads or writes another program's memory", 3));
        if (Has(names, InjectionApis)) result.Signals.Add(("can push code into another program", 3));
        if (Has(names, KernelApis)) result.Signals.Add(("uses kernel routines to reach another program's memory", 3));
        result.UsesMemory = result.Signals.Count > 0;
        if (result.UsesMemory)
        {
            if (Has(names, FindProgramApis)) result.Signals.Add(("looks for another program by name", 1));
            if (Has(names, OverlayApis)) result.Signals.Add(("can draw a see-through window over the screen", 1));
            if (Has(names, InputApis)) result.Signals.Add(("can send mouse or keyboard input", 1));
            if (result.Protector is not null) result.Signals.Add(($"is packed with a protector ({result.Protector})", 1));
            // Cheats are given random names so no list of names can catch them: a name made of digits or random letters is itself a sign.
            if (LooksRandom(Path.GetFileNameWithoutExtension(path))) result.Signals.Add(("has a random-looking name", 1));
            if (RunsFromUserFolder(path)) result.Signals.Add(("sits in Downloads, Desktop, Temp or AppData", 1));
        }
        var hint = new[] { name, Path.GetFileName(Path.GetDirectoryName(path) ?? ""), Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path) ?? "") ?? "") }
            .Select(part => Rules.Match(rules.CheatNames, part) ?? Rules.Match(rules.NameKeywords, part)).FirstOrDefault(match => match is not null);
        result.Hinted = hint is not null;

        if (!result.UsesMemory)
        {
            result.Reason = "Not flagged: it does not reach into another program's memory. If this is a cheat it may call Windows directly, use a driver, or load that code from somewhere else.";
            return result;
        }

        // It can reach into other programs: does it aim at CS2?
        var game = FindWords(path, rules.GameMarkers, ignoreCase: true);
        var offsets = FindWords(path, rules.OffsetMarkers, ignoreCase: false);
        if (game is null || offsets is null)
        {
            result.Reason = "Could not read the file's contents to look for CS2 names.";
            return result;
        }
        result.ReadText = true;
        result.GameMarkers.AddRange(rules.GameMarkers.Where(game.Contains));
        result.OffsetMarkers.AddRange(rules.OffsetMarkers.Where(offsets.Contains));
        if (result.GameMarkers.Count > 0) result.Signals.Add(($"names {string.Join(", ", result.GameMarkers.Take(3))}", 2));
        if (result.OffsetMarkers.Count >= 2) result.Signals.Add(($"carries CS2 offsets ({string.Join(", ", result.OffsetMarkers.Take(4))})", 4));

        var aimsAtCs2 = result.GameMarkers.Count > 0 || result.OffsetMarkers.Count >= 2;
        var detection = aimsAtCs2 && result.Score >= (result.Hinted ? 6 : 7);
        var suspicion = result.Score >= (aimsAtCs2 ? 5 : 6) - (result.Hinted ? 1 : 0);
        var summary = $"Unsigned program, {result.Score} points: {string.Join("; ", result.Signals.Select(signal => signal.What))}";
        if (detection)
        {
            result.Finding = new Finding(name, "file", Finding.Detection, path, summary);
            result.Reason = $"DETECTION: {result.Score} points and it aims at CS2. {string.Join("; ", result.Signals.Select(signal => $"{signal.What} (+{signal.Points})"))}.";
        }
        else if (suspicion)
        {
            result.Finding = new Finding(name, "file", Finding.Suspicion, path, summary);
            result.Reason = $"SUSPICION: {result.Score} points. {string.Join("; ", result.Signals.Select(signal => $"{signal.What} (+{signal.Points})"))}.";
        }
        else
        {
            result.Reason = $"Not flagged: {result.Score} points ({string.Join("; ", result.Signals.Select(signal => signal.What))}); a verdict needs more.";
        }
        return result;
    }

    /// <summary>The result of <see cref="Inspect"/> as lines of text a person can read.</summary>
    public static string Explain(Inspection result)
    {
        var lines = new List<string>
        {
            $"File: {result.Path}",
            $"Size: {result.Length:N0} bytes",
            $"Looked at by the scan: {(result.Candidate ? "yes" : "no")}",
        };
        if (result.IsProgram)
        {
            lines.Add($"Digital signature: {(result.Signed ? "YES (signed programs are skipped)" : "no")}");
            lines.Add($"Kind: {(result.Managed ? ".NET program" : "native program")}");
            lines.Add($"Sections: {string.Join(" ", result.Sections)}{(result.Protector is not null ? $"   <- protector: {result.Protector}" : "")}");
            lines.Add($"Imported names: {result.ImportCount}");
            lines.Add($"Opens other processes (OpenProcess): {(result.OpensProcesses ? "yes" : "no")}");
            lines.Add($"Memory functions: {(result.MemoryApis.Count == 0 ? "none" : string.Join(", ", result.MemoryApis))}");
            lines.Add($"Its name or folder looks like a cheat's (only lowers the bar): {(result.Hinted ? "yes" : "no")}");
            if (result.ReadText)
            {
                lines.Add($"CS2 names found inside: {(result.GameMarkers.Count == 0 ? "none" : string.Join(", ", result.GameMarkers))}");
                lines.Add($"CS2 offset names found inside: {(result.OffsetMarkers.Count == 0 ? "none" : string.Join(", ", result.OffsetMarkers))}");
            }
            lines.Add($"Points: {result.Score}" + (result.Signals.Count == 0 ? "" : "   " + string.Join(" | ", result.Signals.Select(signal => $"{signal.What} +{signal.Points}"))));
        }
        lines.Add("");
        lines.Add(result.Reason);
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Which of the words are inside the file, as plain text or as the 2-bytes-per-letter text Windows programs also use. The file is read once,
    /// in pieces, so a big program never has to fit in memory. Null when the file cannot be read.
    /// </summary>
    private static HashSet<string>? FindWords(string path, IEnumerable<string> words, bool ignoreCase)
    {
        var patterns = new List<(byte[] Bytes, string Word)>();
        foreach (var word in words)
        {
            if (word.Length < 4) continue;
            var forms = ignoreCase ? new[] { word, word.ToLowerInvariant(), word.ToUpperInvariant() }.Distinct() : new[] { word };
            foreach (var form in forms)
            {
                patterns.Add((Encoding.ASCII.GetBytes(form), word));
                patterns.Add((Encoding.Unicode.GetBytes(form), word));
            }
        }
        var found = new HashSet<string>();
        if (patterns.Count == 0) return found;
        try
        {
            const int Piece = 4 * 1024 * 1024;
            var overlap = patterns.Max(pattern => pattern.Bytes.Length) - 1;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
            if (stream.Length > MaxDeepBytes) return null;
            var buffer = new byte[Piece + overlap];
            var kept = 0;
            while (true)
            {
                var read = stream.Read(buffer, kept, Piece);
                if (read <= 0) break;
                var span = buffer.AsSpan(0, kept + read);
                foreach (var (bytes, word) in patterns)
                {
                    if (!found.Contains(word) && span.IndexOf(bytes) >= 0) found.Add(word);
                }
                // The end of this piece starts the next one, so a word cut in two is still found.
                kept = Math.Min(overlap, span.Length);
                span[^kept..].CopyTo(buffer);
            }
            return found;
        }
        catch
        {
            return null;
        }
    }
}
