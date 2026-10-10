using System.IO;
using System.Text;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// Looks at what a program IS instead of what it is called, and writes down facts: which Windows functions it uses and which words it holds from the
/// lists the server sent. It does not judge. The points, the limits and which words belong to which cheat are on the server, so a person who takes this
/// program apart finds what it reports and nothing about what counts. Windows' own programs are left alone.
/// </summary>
public static class ContentAnalyzer
{
    private const long MaxDeepBytes = 64L * 1024 * 1024;

    private static readonly string[] Extensions = { ".exe", ".dll", ".sys" };

    public static bool IsCandidate(string path, long length)
    {
        if (length < 16 * 1024 || length > MaxDeepBytes) return false;
        if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
        // Windows' own programs are signed and are the largest part of any disk.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return !(windows.Length > 0 && path.StartsWith(windows, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Everything the content check learned about one file.</summary>
    public sealed class Inspection
    {
        public string Path { get; init; } = "";
        public long Length { get; init; }
        public bool Candidate { get; init; }
        public bool IsProgram { get; set; }
        public bool Signed { get; set; }
        public bool Managed { get; set; }
        public List<string> Sections { get; } = new();
        public int ImportCount { get; set; }
        /// <summary>What goes to the server about this file; null when there is nothing worth reporting.</summary>
        public ProgramFact? Fact { get; set; }
        public string Reason { get; set; } = "";
    }

    public static void Analyze(ScanContext context, string path, long length, bool running = false)
    {
        var inspection = Inspect(context.Rules, path, length, running);
        if (inspection.Fact is not null) context.AddFact(inspection.Fact);
    }

    /// <summary>Looks at one file the way the scan does. Also used by --explain on a test file.</summary>
    public static Inspection Inspect(Rules rules, string path, long length, bool running = false)
    {
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

        var fact = new ProgramFact
        {
            Name = Path.GetFileName(path),
            Path = path,
            Size = length,
            Signed = pe.IsSigned,
            Managed = pe.IsManaged,
            Running = running ? true : null,
            Protector = pe.SectionNames.FirstOrDefault(section => rules.ProtectorSections.Contains(section, StringComparer.OrdinalIgnoreCase)),
        };

        // Which of the listed functions it uses: from its import table, or for a .NET program from the names in its own data. Signed programs are not asked.
        if (!pe.IsSigned)
        {
            if (pe.IsManaged)
            {
                if (FindWords(path, rules.Apis, ignoreCase: true) is { } found) fact.Apis.AddRange(found.Select(api => api.ToLowerInvariant()).Distinct());
            }
            else
            {
                fact.Apis.AddRange(pe.Imports.Where(rules.IsProbeApi).Select(api => api.ToLowerInvariant()).Distinct());
            }
        }

        // Which of the listed words it holds: the game's names and offsets for a program that uses the functions, and the words staff gave to recognise a cheat in any program.
        var reaches = !pe.IsSigned && fact.Apis.Count > 0;
        if (reaches && rules.GameMarkers.Count > 0 && FindWords(path, rules.GameMarkers, ignoreCase: true) is { } game) fact.Game.AddRange(rules.GameMarkers.Where(game.Contains));
        var exact = new List<string>();
        if (reaches) exact.AddRange(rules.OffsetMarkers);
        exact.AddRange(rules.FamilyStrings);
        if (exact.Count > 0 && FindWords(path, exact, ignoreCase: false) is { } inside)
        {
            if (reaches) fact.Offsets.AddRange(rules.OffsetMarkers.Where(inside.Contains));
            fact.Family.AddRange(rules.FamilyStrings.Where(inside.Contains));
        }

        if (fact.Apis.Count >= 2 || fact.Family.Count > 0 || fact.Game.Count > 0 || fact.Offsets.Count > 0)
        {
            result.Fact = fact;
            result.Reason = "Reported to the server (it makes the verdict).";
        }
        else
        {
            result.Reason = pe.IsSigned ? "Nothing to report: the program is signed and holds none of the listed words." : "Nothing to report: it uses too few of the listed functions and holds none of the listed words.";
        }
        return result;
    }

    /// <summary>The result of <see cref="Inspect"/> as lines of text a person can read. The program does not judge: the server does.</summary>
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
            lines.Add($"Digital signature: {(result.Signed ? "yes" : "no")}");
            lines.Add($"Kind: {(result.Managed ? ".NET program" : "native program")}");
            lines.Add($"Sections: {string.Join(" ", result.Sections)}");
            lines.Add($"Imported names: {result.ImportCount}");
        }
        if (result.Fact is { } fact)
        {
            lines.Add("");
            lines.Add("What would be sent to the server:");
            lines.Add($"  functions: {(fact.Apis.Count == 0 ? "none" : string.Join(", ", fact.Apis))}");
            lines.Add($"  game names: {(fact.Game.Count == 0 ? "none" : string.Join(", ", fact.Game))}");
            lines.Add($"  offset names: {(fact.Offsets.Count == 0 ? "none" : string.Join(", ", fact.Offsets))}");
            lines.Add($"  other listed words: {fact.Family.Count}");
            lines.Add($"  protector: {fact.Protector ?? "none"}");
        }
        lines.Add("");
        lines.Add(result.Reason);
        lines.Add("The verdict (detection, suspicion or nothing) is made by the LEGACY-X server from these facts and is shown in the Checks page.");
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
