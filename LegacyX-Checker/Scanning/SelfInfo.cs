using System.IO;
using System.Security.Cryptography;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// The checker's own files must never be reported as a finding: its rules list the very words a cheat carries, and its report says what it
/// found. A program is recognised by its exact path or its exact SHA-256 (a name alone is not enough: a cheat could be called anything),
/// the files it keeps next to itself by their exact names.
/// </summary>
public static class SelfInfo
{
    private static readonly string? ExePath = Environment.ProcessPath;
    private static readonly string Folder = AppContext.BaseDirectory.TrimEnd('\\');
    private static readonly Lazy<(long Length, string? Hash)> Self = new(() =>
    {
        try
        {
            if (string.IsNullOrEmpty(ExePath)) return (0, null);
            using var stream = new FileStream(ExePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
            return (stream.Length, Convert.ToHexString(SHA256.HashData(stream)));
        }
        catch
        {
            return (0, null);
        }
    });

    public static bool IsOwn(string path, long? length = null)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        // What the program writes for the player: the report and the explanation on the Desktop.
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && (name.Equals("LegacyX-Checker-report.txt", StringComparison.OrdinalIgnoreCase) || name.Equals("LegacyX-Checker-explain.txt", StringComparison.OrdinalIgnoreCase))) return true;
        // What sits next to the program: its rules, its code and its marks.
        var directory = Path.GetDirectoryName(path)?.TrimEnd('\\') ?? "";
        if (directory.Equals(Folder, StringComparison.OrdinalIgnoreCase)
            && (name.Equals("rules.json", StringComparison.OrdinalIgnoreCase) || name.Equals("check.json", StringComparison.OrdinalIgnoreCase)
                || name.Equals("checker.json", StringComparison.OrdinalIgnoreCase) || name.StartsWith("used", StringComparison.OrdinalIgnoreCase) && extension.Equals(".flag", StringComparison.OrdinalIgnoreCase))) return true;
        if (!string.IsNullOrEmpty(ExePath) && path.Equals(ExePath, StringComparison.OrdinalIgnoreCase)) return true;
        // A copy of the program (same size, same SHA-256) anywhere else.
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var size = length ?? new FileInfo(path).Length;
            if (size != Self.Value.Length || Self.Value.Hash is null) return false;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(Self.Value.Hash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
