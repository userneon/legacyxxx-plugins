using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// Whether the kernel anti-cheats FACEIT AC and Riot Vanguard are on this PC and switched on (owner request 2026-10-26). Read from the Windows
/// service list in the registry, the programs running now and the install folder. Nothing is changed and no file is opened.
/// </summary>
public static class AntiCheatScanner
{
    private sealed record Product(string Name, string[] Services, string[] Processes, string[] Folders);

    private static readonly Product[] Products =
    {
        new("FACEIT AC", new[] { "FACEIT" }, new[] { "FACEITService", "FACEIT" }, new[] { "FACEIT AC", "FACEIT" }),
        new("Riot Vanguard", new[] { "vgc", "vgk" }, new[] { "vgtray" }, new[] { "Riot Vanguard" }),
    };

    public static void Scan(ScanContext context)
    {
        foreach (var product in Products)
        {
            try
            {
                var starts = product.Services.Select(ServiceStart).Where(start => start is not null).Select(start => start!.Value).ToList();
                var installed = starts.Count > 0 || product.Folders.Any(FolderExists);
                var running = product.Processes.Any(name => Process.GetProcessesByName(name).Length > 0);
                // Start: 0 boot, 1 system, 2 automatic, 3 manual, 4 disabled. "On" means it loads by itself (0-2); a manual one only loads when its game starts.
                var load = starts.Count == 0 ? null : starts.Min() switch { 0 or 1 => "boot", 2 => "automatic", 3 => "manual", _ => "disabled" };
                context.AntiCheats.Add(new AntiCheatInfo { Name = product.Name, Installed = installed, Load = load, Running = running });
                context.Log($"[ ok ] anti-cheat: {product.Name} {(installed ? (running ? "running" : "installed") : "not installed")}");
            }
            catch
            {
                // Not readable: leave it out rather than say it is off.
            }
        }
    }

    private static int? ServiceStart(string service)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{service}");
        return key?.GetValue("Start") is int start ? start : null;
    }

    private static bool FolderExists(string folder) =>
        Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), folder))
        || Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), folder));
}
