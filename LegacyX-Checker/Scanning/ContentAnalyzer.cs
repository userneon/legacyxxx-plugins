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

    // Functions a program uses to read or write the memory of another program, or to push code into it.
    private static readonly string[] MemoryApis =
    {
        "readprocessmemory", "writeprocessmemory", "ntreadvirtualmemory", "ntwritevirtualmemory", "virtualallocex",
        "createremotethread", "ntcreatethreadex", "queueuserapc", "setwindowshookexa", "setwindowshookexw",
    };

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
        public Finding? Finding { get; set; }
        /// <summary>In plain words: why this file was or was not flagged.</summary>
        public string Reason { get; set; } = "";
    }

    public static void Analyze(ScanContext context, string path)
    {
        var inspection = Inspect(context.Rules, path, new FileInfo(path).Length);
        if (inspection.Finding is not null) context.Add(inspection.Finding);
    }

    /// <summary>Looks at one file the way the scan does, and writes down every step. Also used by --explain on a test file.</summary>
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
        result.OpensProcesses = pe.Imports.Contains("openprocess", StringComparer.OrdinalIgnoreCase);
        result.MemoryApis.AddRange(MemoryApis.Where(api => pe.Imports.Contains(api, StringComparer.OrdinalIgnoreCase)));

        result.Protector = pe.SectionNames.FirstOrDefault(section => rules.ProtectorSections.Contains(section, StringComparer.OrdinalIgnoreCase));
        if (pe.IsSigned)
        {
            result.Reason = "Not flagged: the program has a digital signature, and signed programs are left alone.";
            return result;
        }
        if (result.Protector is not null)
        {
            result.Finding = new Finding(name, "file", Finding.Suspicion, path, $"Unsigned program packed with a protector (section \"{result.Protector}\")");
        }

        var usesMemory = result.OpensProcesses && result.MemoryApis.Count > 0;
        // A .NET program names its functions in its own data, not in the import table: look at its text instead.
        byte[]? text = null;
        if (!usesMemory && pe.IsManaged)
        {
            text = ReadText(path);
            if (text is not null) usesMemory = MemoryApis.Any(api => ContainsAscii(text, api, ignoreCase: true));
        }
        result.UsesMemory = usesMemory;
        if (!usesMemory)
        {
            result.Reason = result.Finding is not null
                ? "Flagged only for the protector. It does not import functions for reading another program's memory (it may call Windows directly, use a driver, or load that code later)."
                : "Not flagged: it does not import the functions a program needs to read another program's memory" + (pe.IsManaged ? " (and its .NET text does not name them either)." : ". If this is a cheat it may call Windows directly, use a driver, or load that code from somewhere else.");
            return result;
        }

        text ??= ReadText(path);
        if (text is null)
        {
            result.Reason = "Could not read the file's contents to look for CS2 names.";
            return result;
        }
        result.ReadText = true;
        result.GameMarkers.AddRange(rules.GameMarkers.Where(marker => ContainsAscii(text, marker, ignoreCase: true)));
        result.OffsetMarkers.AddRange(rules.OffsetMarkers.Where(marker => ContainsAscii(text, marker, ignoreCase: false)));
        if (result.OffsetMarkers.Count >= 2)
        {
            result.Finding = new Finding(name, "file", Finding.Detection, path, $"Unsigned program that reads other programs' memory and carries CS2 offsets ({string.Join(", ", result.OffsetMarkers.Take(4))})");
            result.Reason = "DETECTION: unsigned, reads other programs' memory, and carries two or more CS2 offset names.";
        }
        else if (result.GameMarkers.Count > 0)
        {
            result.Finding = new Finding(name, "file", Finding.Suspicion, path, $"Unsigned program that reads other programs' memory and names {string.Join(", ", result.GameMarkers.Take(3))}");
            result.Reason = "SUSPICION: unsigned, reads other programs' memory, and names CS2 (cs2.exe / client.dll …), but has fewer than two offset names.";
        }
        else
        {
            result.Reason = "Not flagged: it reads other programs' memory, but no CS2 names or offsets were found inside (they may be encrypted, or the target is not CS2).";
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
            lines.Add($"Memory functions imported: {(result.MemoryApis.Count == 0 ? "none" : string.Join(", ", result.MemoryApis))}");
            lines.Add($"Reads other programs' memory (by the scan's rule): {(result.UsesMemory ? "yes" : "no")}");
            if (result.ReadText)
            {
                lines.Add($"CS2 names found inside: {(result.GameMarkers.Count == 0 ? "none" : string.Join(", ", result.GameMarkers))}");
                lines.Add($"CS2 offset names found inside: {(result.OffsetMarkers.Count == 0 ? "none" : string.Join(", ", result.OffsetMarkers))}");
            }
        }
        lines.Add("");
        lines.Add(result.Reason);
        return string.Join("\n", lines);
    }

    private static byte[]? ReadText(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > MaxDeepBytes) return null;
            var buffer = new byte[stream.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = stream.Read(buffer, read, buffer.Length - read);
                if (chunk == 0) break;
                read += chunk;
            }
            return buffer;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Is the word inside the file as plain text, or as the 2-bytes-per-letter text Windows programs also use?</summary>
    private static bool ContainsAscii(byte[] data, string word, bool ignoreCase)
    {
        if (word.Length < 4) return false;
        var forms = ignoreCase ? new[] { word, word.ToLowerInvariant(), word.ToUpperInvariant() } : new[] { word };
        foreach (var form in forms.Distinct())
        {
            if (data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(form)) >= 0) return true;
            if (data.AsSpan().IndexOf(Encoding.Unicode.GetBytes(form)) >= 0) return true;
        }
        return false;
    }
}
