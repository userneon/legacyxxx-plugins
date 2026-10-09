using System.IO;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>What ran on this PC recently (Prefetch and the Recent list), and whether someone cleaned up after themselves.</summary>
public static class TraceScanner
{
    public static void Scan(ScanContext context)
    {
        ScanPrefetch(context);
        ScanRecent(context);
    }

    private static void ScanPrefetch(ScanContext context)
    {
        try
        {
            var enabled = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", null);
            if (enabled is int value && value == 0)
            {
                context.Add(new Finding("Prefetch is turned off", "tamper", Finding.Suspicion, null, "Windows keeps no record of the programs that ran. This is off on some servers but normally on."));
                return;
            }
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            if (!Directory.Exists(directory)) return;
            var files = Directory.EnumerateFiles(directory, "*.pf").ToList();
            if (files.Count == 0)
            {
                context.Add(new Finding("Prefetch is empty", "tamper", Finding.Suspicion, null, "No program records at all. Either it was cleared, or this scan could not read it (run it as administrator)."));
                return;
            }
            foreach (var file in files)
            {
                context.CountFile();
                var name = Path.GetFileNameWithoutExtension(file);
                var hit = Rules.Match(context.Rules.NameKeywords, name);
                if (hit is not null) context.Add(new Finding(name, "trace", Finding.Suspicion, file, $"Ran before; matches \"{hit}\""));
            }
        }
        catch (UnauthorizedAccessException)
        {
            context.Add(new Finding("Prefetch could not be read", "trace", Finding.Suspicion, null, "Run the checker as administrator to read what ran recently."));
        }
        catch
        {
            // Not readable for another reason: skip.
        }
    }

    private static void ScanRecent(ScanContext context)
    {
        try
        {
            var directory = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory, "*.lnk"))
            {
                context.CountFile();
                var name = Path.GetFileNameWithoutExtension(file);
                var hit = Rules.Match(context.Rules.NameKeywords, name);
                if (hit is not null) context.Add(new Finding(name, "trace", Finding.Suspicion, file, $"Opened recently; matches \"{hit}\""));
            }
        }
        catch
        {
            // Skip.
        }
    }
}
