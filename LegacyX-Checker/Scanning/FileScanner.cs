using System.IO;
using System.Threading;
using System.Security.Cryptography;

namespace LegacyX.Checker.Scanning;

/// <summary>Walks the fixed drives and compares file names (and, for programs, a hash) with the rules. File contents are never sent.</summary>
public static class FileScanner
{
    private static readonly string[] Programs = { ".exe", ".dll", ".sys", ".bat", ".cmd", ".ps1", ".jar" };
    private const long MaxHashBytes = 64L * 1024 * 1024;

    // Folders that are huge and hold nothing a player put there.
    private static bool Skip(string directory)
    {
        var name = Path.GetFileName(directory);
        return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WinSxS", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase);
    }

    public static void Scan(ScanContext context, CancellationToken cancel)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            var pending = new Stack<string>();
            pending.Push(drive.RootDirectory.FullName);
            while (pending.Count > 0)
            {
                cancel.ThrowIfCancellationRequested();
                var directory = pending.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory)) CheckFile(context, file);
                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        if (Skip(child)) continue;
                        CheckFolder(context, child);
                        // Junctions and links can loop back on themselves.
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                        pending.Push(child);
                    }
                }
                catch
                {
                    // A folder we are not allowed into is skipped.
                }
            }
        }
    }

    /// <summary>A folder named like a cheat is worth a look even when the files inside have harmless names.</summary>
    private static void CheckFolder(ScanContext context, string path)
    {
        var name = Path.GetFileName(path);
        var hit = Rules.Match(context.Rules.NameKeywords, name);
        if (hit is not null) context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Folder name matches \"{hit}\""));
    }

    private static void CheckFile(ScanContext context, string path)
    {
        context.CountFile();
        var name = Path.GetFileName(path);
        if (context.Rules.IsKnownFileName(name))
        {
            context.Add(new Finding(name, "file", Finding.Detection, path, "A file name on the list of known cheats"));
            return;
        }
        var hit = Rules.Match(context.Rules.NameKeywords, name);
        if (hit is not null)
        {
            context.Add(new Finding(name, "file", Finding.Suspicion, path, $"Name matches \"{hit}\""));
        }
        if (!context.Rules.HasHashes) return;
        var extension = Path.GetExtension(path);
        if (!Programs.Contains(extension, StringComparer.OrdinalIgnoreCase)) return;
        try
        {
            var info = new FileInfo(path);
            if (info.Length == 0 || info.Length > MaxHashBytes) return;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (context.Rules.IsKnownHash(hash)) context.Add(new Finding(name, "file", Finding.Detection, path, "Matches the fingerprint of a known cheat"));
        }
        catch
        {
            // In use or not readable: skip.
        }
    }
}
