using System.Collections.Concurrent;
using System.IO;
using System.IO.Enumeration;
using System.Threading;
using System.Security.Cryptography;

namespace LegacyX.Checker.Scanning;

/// <summary>Walks the fixed drives and looks at what each program is and does. Only an exact known name or fingerprint (from staff) is judged by name. File contents are never sent.</summary>
public static class FileScanner
{
    private static readonly string[] Programs = { ".exe", ".dll", ".sys", ".bat", ".cmd", ".ps1", ".jar" };
    private const long MaxHashBytes = 64L * 1024 * 1024;

    // Folders that are huge and hold nothing a player put there.
    private static bool Skip(string directory)
    {
        var name = Path.GetFileName(directory);
        if (name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.Equals("WinSxS", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".git", StringComparison.OrdinalIgnoreCase)) return true;
        // Windows' own servicing stores: thousands of signed files and no place for a player's.
        var parent = Path.GetFileName(Path.GetDirectoryName(directory) ?? "");
        return (name.Equals("assembly", StringComparison.OrdinalIgnoreCase) || name.Equals("servicing", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Installer", StringComparison.OrdinalIgnoreCase) || name.Equals("DriverStore", StringComparison.OrdinalIgnoreCase))
               && (parent.Equals("Windows", StringComparison.OrdinalIgnoreCase) || parent.Equals("System32", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The places a player's own downloads, tools and leftovers are: read first, so the likely findings come early.</summary>
    private static IEnumerable<string> HotFolders()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new[]
        {
            Path.Combine(profile, "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Path.Combine(local, "Temp"), roaming, local,
            Path.Combine(Path.GetPathRoot(profile) ?? "C:\\", "Users", "Public"), Path.Combine(Path.GetPathRoot(profile) ?? "C:\\", "ProgramData"),
        };
        return candidates.Where(path => !string.IsNullOrEmpty(path) && Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static void Scan(ScanContext context, CancellationToken cancel)
    {
        // Warm what the workers share, so they only read it.
        context.Rules.IsKnownHash("");
        context.Rules.IsKnownFileName("");
        var visited = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        try
        {
            context.Log("[ .. ] files: the places downloads and tools usually go");
            Walk(context, HotFolders(), visited, cancel);
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                context.Log($"[ .. ] files: reading drive {drive.Name}");
                Walk(context, new[] { drive.RootDirectory.FullName }, visited, cancel);
            }
        }
        catch (AggregateException failure) when (failure.InnerExceptions.All(inner => inner is OperationCanceledException))
        {
            throw new OperationCanceledException(cancel);
        }
    }

    /// <summary>Reads the folders under <paramref name="roots"/> on several threads at once (all but one of the CPU's cores).</summary>
    private static void Walk(ScanContext context, IEnumerable<string> roots, ConcurrentDictionary<string, byte> visited, CancellationToken cancel)
    {
        var stack = new ConcurrentStack<string>();
        var pending = 0;
        foreach (var root in roots)
        {
            Interlocked.Increment(ref pending);
            stack.Push(root);
        }
        if (pending == 0) return;
        var workers = Math.Max(2, Environment.ProcessorCount - 1);
        var tasks = new Task[workers];
        for (var index = 0; index < workers; index++)
        {
            tasks[index] = Task.Factory.StartNew(() =>
            {
                var wait = new SpinWait();
                while (Volatile.Read(ref pending) > 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (!stack.TryPop(out var directory))
                    {
                        wait.SpinOnce();
                        continue;
                    }
                    try
                    {
                        if (visited.TryAdd(directory, 0)) Visit(context, directory, stack, ref pending);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // A folder we are not allowed into is skipped.
                    }
                    finally
                    {
                        Interlocked.Decrement(ref pending);
                    }
                }
            }, cancel, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        Task.WaitAll(tasks);
    }

    private static void Visit(ScanContext context, string directory, ConcurrentStack<string> stack, ref int pending)
    {
        context.CurrentPath = directory;
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0, RecurseSubdirectories = false, ReturnSpecialDirectories = false };
        // The size comes with the listing, so no file is asked for twice.
        var entries = new FileSystemEnumerable<(string Path, long Length, bool IsDirectory, bool IsLink)>(
            directory,
            (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory ? 0L : entry.Length, entry.IsDirectory, (entry.Attributes & FileAttributes.ReparsePoint) != 0),
            options);
        var files = new List<(string Path, long Length)>();
        var folders = new List<(string Path, bool IsLink)>();
        foreach (var entry in entries)
        {
            if (entry.IsDirectory) folders.Add((entry.Path, entry.IsLink));
            else files.Add((entry.Path, entry.Length));
        }
        foreach (var file in files) CheckFile(context, file.Path, file.Length);
        foreach (var (child, isLink) in folders)
        {
            if (Skip(child)) continue;
            // Junctions and links can loop back on themselves.
            if (isLink) continue;
            Interlocked.Increment(ref pending);
            stack.Push(child);
        }
    }

    private static void CheckFile(ScanContext context, string path, long length)
    {
        context.CountFile();
        if (SelfInfo.IsOwn(path, length)) return;
        var name = Path.GetFileName(path);
        if (context.Rules.IsKnownFileName(name))
        {
            context.Add(new Finding(name, "file", Finding.Detection, path, "A file name on the list of known cheats"));
            return;
        }
        // What the program is and does, whatever it is called. A name alone is never a finding.
        try
        {
            if (ContentAnalyzer.IsCandidate(path, length)) ContentAnalyzer.Analyze(context, path, length);
        }
        catch
        {
            // Skip a file that cannot be read.
        }
        if (!context.Rules.HasHashes) return;
        var extension = Path.GetExtension(path);
        if (!Programs.Contains(extension, StringComparer.OrdinalIgnoreCase)) return;
        try
        {
            if (length == 0 || length > MaxHashBytes) return;
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
