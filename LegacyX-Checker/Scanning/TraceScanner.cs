using System.IO;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>Whether Windows still keeps a record of what ran (Prefetch), and whether someone cleaned up after themselves. Names in those records are not judged.</summary>
public static class TraceScanner
{
    public static void Scan(ScanContext context)
    {
        ScanPrefetch(context);
        ScanRecent(context);
        context.Log("[ ok ] traces: prefetch and recent files read");
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
            // The records only name programs that ran; a name proves nothing, so it is not judged here. A program that is still on the disk is
            // looked at for what it does by the file scan; one that was deleted leaves only a name.
            context.CountFile();
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
            // Recent only lists names of what was opened: not judged (see Prefetch above).
            context.CountFile();
        }
        catch
        {
            // Skip.
        }
    }
}
