using System.IO;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// Looks at a whole folder, not one file: a cheat usually comes as a folder (the program, its config, a readme), and its name, its
/// file names and the words in its small text files say as much as the program does.
/// </summary>
public static class FolderInspector
{
    private static readonly string[] Programs = { ".exe", ".dll", ".sys", ".bat", ".cmd", ".ps1", ".jar", ".vbs" };
    private static readonly string[] TextKinds = { ".txt", ".cfg", ".ini", ".json", ".lua", ".md", ".xml", ".yaml", ".yml", ".toml", ".log", ".conf", ".nfo" };
    private const long MaxTextBytes = 256 * 1024;
    private const int MaxFiles = 500;

    private static bool IsProgram(string file) => Programs.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    /// <summary>The distinct cheat-feature words (aimbot, wallhack …) that appear in the text.</summary>
    private static void AddFeatures(Rules rules, string text, HashSet<string> into)
    {
        foreach (var word in rules.FeatureWords)
        {
            if (word.Length >= 4 && text.Contains(word, StringComparison.OrdinalIgnoreCase)) into.Add(word.ToLowerInvariant());
        }
    }

    private static string? ReadSmallText(string file)
    {
        try
        {
            if (!TextKinds.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) return null;
            if (new FileInfo(file).Length > MaxTextBytes) return null;
            return File.ReadAllText(file);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// A folder whose name is a known cheat's name (Undetek …). Nothing inside, or a program inside, or a file that talks about cheating
    /// inside: that is the cheat. Only harmless-looking files inside: a suspicion.
    /// </summary>
    public static void CheckNamedFolder(ScanContext context, string path, string name, string hit, bool known)
    {
        var files = new List<string>();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                files.Add(file);
                if (files.Count >= MaxFiles) break;
            }
        }
        catch
        {
            // Not allowed in: the name alone is what we have.
        }

        files.RemoveAll(file => SelfInfo.IsOwn(file));
        if (!known)
        {
            context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Folder name matches \"{hit}\""));
            return;
        }
        if (files.Count == 0)
        {
            context.Add(new Finding(name, "file", Finding.Detection, path, $"Folder is named after a known cheat (\"{hit}\") and nothing is inside"));
            return;
        }
        var program = files.FirstOrDefault(IsProgram);
        if (program is not null)
        {
            context.Add(new Finding(name, "file", Finding.Detection, path, $"Folder is named after a known cheat (\"{hit}\") and holds a program ({Path.GetFileName(program)})"));
            return;
        }
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var features = new HashSet<string>();
            AddFeatures(context.Rules, fileName, features);
            if (ReadSmallText(file) is { } text) AddFeatures(context.Rules, text, features);
            if (features.Count > 0 || Rules.Match(context.Rules.NameKeywords, fileName) is not null)
            {
                context.Add(new Finding(name, "file", Finding.Detection, path, $"Folder is named after a known cheat (\"{hit}\") and holds {fileName}"));
                return;
            }
        }
        context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Folder is named after a known cheat (\"{hit}\"), but only ordinary-looking files are inside"));
    }

    /// <summary>
    /// A folder with any name: its small text and config files are read. Two or more different cheat features named there (aimbot, wallhack …) is a
    /// suspicion, and with an unsigned program in the same folder it is a detection.
    /// </summary>
    public static void CheckContents(ScanContext context, string directory, IReadOnlyList<string> files)
    {
        files = files.Where(file => !SelfInfo.IsOwn(file)).ToList();
        var texts = files.Where(file => TextKinds.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).Take(30).ToList();
        if (texts.Count == 0) return;
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (windows.Length > 0 && directory.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) return;

        var features = new HashSet<string>();
        foreach (var file in files) AddFeatures(context.Rules, Path.GetFileName(file), features);
        foreach (var file in texts)
        {
            // A list of cheat words (a copy of the checker's rules) names them without being one.
            if (ReadSmallText(file) is { } text && !text.Contains("\"offsetMarkers\"", StringComparison.Ordinal)) AddFeatures(context.Rules, text, features);
        }
        if (features.Count < 2) return;

        var unsigned = files
            .Where(file => Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .Select(file => (file, pe: PeFile.Read(file)))
            .FirstOrDefault(item => item.pe is { IsSigned: false });
        var folderName = Path.GetFileName(directory.TrimEnd('\\'));
        var list = string.Join(", ", features.Take(4));
        // A program next to a couple of cheat words is a detection only with three or more; fewer is a suspicion for a person to look at.
        if (unsigned.file is not null && features.Count >= 3)
        {
            context.Add(new Finding(folderName, "file", Finding.Detection, directory, $"Unsigned program ({Path.GetFileName(unsigned.file)}) next to files that talk about {list}"));
        }
        else
        {
            context.Add(new Finding(folderName, "file", Finding.Suspicion, directory, $"Files in this folder talk about {list}"));
        }
    }
}
