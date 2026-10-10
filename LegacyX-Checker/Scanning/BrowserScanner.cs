using System.IO;
using Microsoft.Data.Sqlite;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// The download list of Chrome, Edge, Brave, Opera and Vivaldi: which program or archive was saved, from which site and whether it is still on the disk.
/// Only that list is read (never the pages visited, passwords, cookies or what is typed), and only the file's name and the site's name are kept.
/// A program that is still on the disk is judged by the file scan like any other; this is for what was downloaded and then deleted.
/// </summary>
public static class BrowserScanner
{
    private static readonly string[] Interesting = { ".exe", ".dll", ".sys", ".msi", ".bat", ".cmd", ".ps1", ".jar", ".zip", ".rar", ".7z" };

    private static IEnumerable<(string Browser, string History)> Histories()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var roots = new (string Browser, string Path)[]
        {
            ("Chrome", Path.Combine(local, "Google", "Chrome", "User Data")),
            ("Edge", Path.Combine(local, "Microsoft", "Edge", "User Data")),
            ("Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")),
            ("Vivaldi", Path.Combine(local, "Vivaldi", "User Data")),
            ("Chromium", Path.Combine(local, "Chromium", "User Data")),
        };
        foreach (var (browser, root) in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var profile in Directory.EnumerateDirectories(root).Where(dir => Path.GetFileName(dir) is "Default" || Path.GetFileName(dir).StartsWith("Profile ", StringComparison.Ordinal)))
            {
                var history = Path.Combine(profile, "History");
                if (File.Exists(history)) yield return (browser, history);
            }
        }
        var opera = Path.Combine(roaming, "Opera Software", "Opera Stable", "History");
        if (File.Exists(opera)) yield return ("Opera", opera);
    }

    public static void Scan(ScanContext context)
    {
        var browsers = 0;
        var downloads = 0;
        var deleted = 0;
        foreach (var (browser, history) in Histories())
        {
            string? copy = null;
            try
            {
                // The browser keeps the file open: read a copy, with its write-ahead file so the newest downloads are in it.
                copy = Path.Combine(Path.GetTempPath(), $"lx-{Guid.NewGuid():N}");
                Copy(history, copy);
                if (File.Exists(history + "-wal")) Copy(history + "-wal", copy + "-wal");
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                // The last address in a download's chain is the file's real source.
                command.CommandText =
                    "SELECT d.target_path, (SELECT c.url FROM downloads_url_chains c WHERE c.id = d.id ORDER BY c.chain_index DESC LIMIT 1) " +
                    "FROM downloads d ORDER BY d.start_time DESC LIMIT 3000";
                using var reader = command.ExecuteReader();
                browsers++;
                while (reader.Read())
                {
                    var path = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    var url = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (path.Length == 0 || !Interesting.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) continue;
                    downloads++;
                    var gone = !File.Exists(path);
                    if (gone) deleted++;
                    var host = Uri.TryCreate(url, UriKind.Absolute, out var address) ? address.Host : "";
                    var listed = host.Length > 0 ? context.Rules.CheatHosts.FirstOrDefault(site => host.Equals(site, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + site, StringComparison.OrdinalIgnoreCase)) : null;
                    if (listed is null) continue;
                    // The site is on staff's list of cheat sites: a download from it is worth a person's look, still on the disk or not.
                    context.Add(new Finding(Path.GetFileName(path), "trace", Finding.Suspicion, path, $"{browser} download from {host} (on the list of cheat sites){(gone ? "; no longer on the disk" : "")}"));
                }
            }
            catch
            {
                // A browser that is locked, or has another layout: skipped.
            }
            finally
            {
                if (copy is not null)
                {
                    foreach (var file in new[] { copy, copy + "-wal" })
                    {
                        try { File.Delete(file); } catch { /* left in Temp */ }
                    }
                }
            }
        }
        context.Log($"[ ok ] browsers: {browsers} read, {downloads} downloaded programs and archives, {deleted} no longer on the disk");
    }

    private static void Copy(string from, string to)
    {
        using var source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = new FileStream(to, FileMode.Create, FileAccess.Write);
        source.CopyTo(target);
    }
}
