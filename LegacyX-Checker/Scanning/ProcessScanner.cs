using System.Diagnostics;

namespace LegacyX.Checker.Scanning;

/// <summary>Programs running right now: each one's file is looked at for what it does, the same as any program on the disk. A name or a window title alone is never a finding.</summary>
public static class ProcessScanner
{
    public static void Scan(ScanContext context)
    {
        var all = Process.GetProcesses();
        context.Log($"[ ok ] processes: {all.Length} running");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in all)
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                if (SelfInfo.IsOwn(path)) continue;
                var length = new System.IO.FileInfo(path).Length;
                if (!ContentAnalyzer.IsCandidate(path, length)) continue;
                var inspection = ContentAnalyzer.Inspect(context.Rules, path, length);
                if (inspection.Finding is { } finding) context.Add(finding with { Kind = "process", Note = "Running now. " + finding.Note });
            }
            catch
            {
                // A process that ends, or one we are not allowed to look into, is skipped.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
