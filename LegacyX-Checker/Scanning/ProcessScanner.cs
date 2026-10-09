using System.Diagnostics;

namespace LegacyX.Checker.Scanning;

/// <summary>Programs running right now whose name or window title matches the rules.</summary>
public static class ProcessScanner
{
    public static void Scan(ScanContext context)
    {
        var all = Process.GetProcesses();
        context.Log($"[ ok ] processes: {all.Length} running");
        foreach (var process in all)
        {
            try
            {
                var name = process.ProcessName;
                var title = process.MainWindowTitle ?? "";
                var hit = Rules.Match(context.Rules.ProcessKeywords, name) ?? Rules.Match(context.Rules.ProcessKeywords, title);
                if (hit is not null)
                {
                    context.Add(new Finding(name + ".exe", "process", Finding.Suspicion, null, $"Running now; matches \"{hit}\""));
                }
            }
            catch
            {
                // A process that ends or cannot be read while we look is skipped.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
