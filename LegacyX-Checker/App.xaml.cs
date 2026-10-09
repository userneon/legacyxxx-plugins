using System.Windows;

namespace LegacyX.Checker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // LegacyX-Checker.exe --explain "C:\path\to\file.exe": say why the content check does or does not flag this one file.
        if (e.Args.Length >= 2 && e.Args[0].Equals("--explain", StringComparison.OrdinalIgnoreCase))
        {
            ExplainFile(e.Args[1]);
            Shutdown();
            return;
        }
        if (AlreadyUsed())
        {
            // The check was sent but the removal did not finish (or someone copied the program back): it does not run again.
            MessageBox.Show("This program has already been used for a check and cannot run again. Ask staff for a new check.", "LEGACY-X Checker", MessageBoxButton.OK, MessageBoxImage.Information);
            RemoveSelf();
            Shutdown();
            return;
        }
        new MainWindow().Show();
    }

    private static void ExplainFile(string path)
    {
        string text;
        try
        {
            var length = new System.IO.FileInfo(path).Length;
            text = Scanning.ContentAnalyzer.Explain(Scanning.ContentAnalyzer.Inspect(Rules.Load(), path, length));
        }
        catch (Exception problem)
        {
            text = $"Could not look at \"{path}\": {problem.Message}";
        }
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LegacyX-Checker-explain.txt"), text);
        }
        catch
        {
            // The Desktop can be read-only: the message box below still shows it.
        }
        MessageBox.Show(text, "LEGACY-X Checker: explain", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public const string Version = "1.0.0";

    /// <summary>The path of check.json next to the program: a personal download carries the check's code in it.</summary>
    public static string CheckFilePath => System.IO.Path.Combine(AppContext.BaseDirectory, "check.json");

    /// <summary>The code from check.json, if the player downloaded the checker for this check (then there is nothing to type).</summary>
    public static string? CodeFromFile()
    {
        try
        {
            if (!System.IO.File.Exists(CheckFilePath)) return null;
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(CheckFilePath));
            return document.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The code is for one check only: once the result is sent, the file is deleted.</summary>
    public static void DeleteCheckFile()
    {
        try
        {
            if (System.IO.File.Exists(CheckFilePath)) System.IO.File.Delete(CheckFilePath);
        }
        catch
        {
            // Read-only folder: the code is already used up on the server anyway.
        }
    }

    /// <summary>
    /// The program is for one check: once its result has been sent it removes itself (the .exe, rules.json and check.json) a moment after
    /// it closes. Only when it really is LegacyX-Checker.exe, so running it from the build tools never deletes anything else.
    /// </summary>
    public static void RemoveSelf()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !System.IO.Path.GetFileName(exe).Equals("LegacyX-Checker.exe", StringComparison.OrdinalIgnoreCase)) return;
            var folder = AppContext.BaseDirectory.TrimEnd('\\');
            var rules = System.IO.Path.Combine(folder, "rules.json");
            var parent = System.IO.Path.GetDirectoryName(folder);
            // The download zips (LegacyX-Checker.zip, "LegacyX-Checker (1).zip", ...) in the places a browser or a player puts them.
            var zips = new List<string>();
            foreach (var place in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), parent, folder })
            {
                if (!string.IsNullOrEmpty(place)) zips.Add($"\"{System.IO.Path.Combine(place, "LegacyX-Checker*.zip")}\"");
            }
            var files = $"\"{exe}\" \"{rules}\" \"{CheckFilePath}\" \"{UsedFlagPath}\" {string.Join(" ", zips)}";
            // Wait for this program to end, delete (bypassing the Recycle Bin), try once more in case a file was still locked, and
            // remove the folder it was unpacked into if nothing else is left in it.
            var command = $"/c ping 127.0.0.1 -n 4 > nul & del /f /q {files} & ping 127.0.0.1 -n 3 > nul & del /f /q {files} & rmdir \"{folder}\"";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", command) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
        }
        catch
        {
            // It stays on the PC; nothing else is affected.
        }
    }

    /// <summary>Written next to the program (and in the user's AppData) once the result is sent: a copy of the program that is still there refuses to run.</summary>
    private static string UsedFlagPath => System.IO.Path.Combine(AppContext.BaseDirectory, "used.flag");

    private static string UsedFlagElsewhere()
    {
        // Keyed by path and the file's creation time, so a fresh download unpacked to the same place for a new check is not blocked.
        var path = Environment.ProcessPath ?? "";
        long created = 0;
        try { created = System.IO.File.GetCreationTimeUtc(path).Ticks; } catch { }
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant() + "|" + created)))[..16];
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LegacyX-Checker", $"used-{key}.flag");
    }

    public static void MarkUsed()
    {
        // Like RemoveSelf: only the real LegacyX-Checker.exe, never a build run.
        if (!System.IO.Path.GetFileName(Environment.ProcessPath ?? "").Equals("LegacyX-Checker.exe", StringComparison.OrdinalIgnoreCase)) return;
        foreach (var path in new[] { UsedFlagPath, UsedFlagElsewhere() })
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                System.IO.File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
            }
            catch
            {
                // The other flag (and the server, which accepts a code once) still hold.
            }
        }
    }

    public static bool AlreadyUsed() => System.IO.File.Exists(UsedFlagPath) || System.IO.File.Exists(UsedFlagElsewhere());

    /// <summary>The API the code is checked against (legacyx.cc itself is the website, not the API). Change it in checker.json next to the program ({ "apiUrl": "..." }) for a test server.</summary>
    public static string ApiUrl()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "checker.json");
            if (System.IO.File.Exists(path))
            {
                using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("apiUrl", out var value) && value.GetString() is { Length: > 0 } url) return url;
            }
        }
        catch
        {
            // A broken file means the normal address.
        }
        return "https://api.legacyx.cc";
    }
}
