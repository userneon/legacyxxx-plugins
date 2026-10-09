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

    public static void Analyze(ScanContext context, string path)
    {
        var pe = PeFile.Read(path);
        if (pe is null) return;
        var rules = context.Rules;
        var name = Path.GetFileName(path);

        // A protector (VMProtect, Themida …) hides what a program does. Cheat loaders use them; so do some honest games.
        var protector = pe.SectionNames.FirstOrDefault(section => rules.ProtectorSections.Contains(section, StringComparer.OrdinalIgnoreCase));
        if (protector is not null && !pe.IsSigned)
        {
            context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Unsigned program packed with a protector (section \"{protector}\")"));
        }
        if (pe.IsSigned) return;

        var usesMemory = pe.Imports.Any(import => MemoryApis.Contains(import, StringComparer.OrdinalIgnoreCase)) && pe.Imports.Contains("openprocess", StringComparer.OrdinalIgnoreCase);
        // A .NET program names its functions in its own data, not in the import table: look at its text instead.
        if (!usesMemory && !pe.IsManaged) return;

        var text = ReadText(path);
        if (text is null) return;
        if (pe.IsManaged) usesMemory = MemoryApis.Any(api => ContainsAscii(text, api, ignoreCase: true));
        if (!usesMemory) return;

        var games = rules.GameMarkers.Where(marker => ContainsAscii(text, marker, ignoreCase: true)).ToList();
        var offsets = rules.OffsetMarkers.Where(marker => ContainsAscii(text, marker, ignoreCase: false)).ToList();
        if (offsets.Count >= 2)
        {
            context.Add(new Finding(name, "file", Finding.Detection, path, $"Unsigned program that reads other programs' memory and carries CS2 offsets ({string.Join(", ", offsets.Take(4))})"));
        }
        else if (games.Count > 0)
        {
            context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Unsigned program that reads other programs' memory and names {string.Join(", ", games.Take(3))}"));
        }
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
