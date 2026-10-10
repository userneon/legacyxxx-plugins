using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace LegacyX.Checker.Scanning;

/// <summary>
/// A fingerprint of this PC's hardware, so staff can tell whether two accounts played on the same machine. Each serial number is hashed on this PC and only
/// the hashes are sent: the numbers themselves never leave it, and a hash cannot be turned back into one.
/// </summary>
public static class HwidCollector
{
    private static readonly string[] Placeholders =
    {
        "TOBEFILLEDBYOEM", "DEFAULTSTRING", "NONE", "NA", "NOTAPPLICABLE", "NOTSPECIFIED", "SYSTEMSERIALNUMBER", "SYSTEMPRODUCTNAME", "UNKNOWN", "OEM", "SERIALNUMBER",
    };

    /// <summary>Upper case, no spaces, dots or dashes; null for a value that says nothing (empty, all zeros or Fs, a maker's placeholder).</summary>
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (text.Length < 4) return null;
        if (text.All(character => character == '0') || text.All(character => character == 'F') || Placeholders.Contains(text)) return null;
        return text;
    }

    private static string Hash(string kind, string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"lx-hwid-1|{kind}|{value}"))).ToLowerInvariant();

    private static IEnumerable<string?> Query(string query, string property)
    {
        var values = new List<string?>();
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            foreach (var item in searcher.Get())
            {
                using (item) values.Add(item[property]?.ToString());
            }
        }
        catch
        {
            // WMI is turned off or not allowed: that part is simply missing.
        }
        return values;
    }

    public static HwidInfo? Collect()
    {
        var parts = new List<HwidPart>();
        var stable = new List<string>();
        void Add(string kind, string? value, bool counts)
        {
            if (Clean(value) is not { } clean) return;
            var hash = Hash(kind, clean);
            if (parts.Any(part => part.Kind == kind && part.Hash == hash)) return;
            parts.Add(new HwidPart { Kind = kind, Hash = hash });
            if (counts) stable.Add(hash);
        }
        foreach (var value in Query("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID").Take(1)) Add("uuid", value, true);
        foreach (var value in Query("SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber").Take(1)) Add("board", value, true);
        foreach (var value in Query("SELECT SerialNumber FROM Win32_BIOS", "SerialNumber").Take(1)) Add("bios", value, true);
        foreach (var value in Query("SELECT ProcessorId FROM Win32_Processor", "ProcessorId").Take(1)) Add("cpu", value, true);
        foreach (var value in Query("SELECT SerialNumber FROM Win32_DiskDrive", "SerialNumber").Take(4)) Add("disk", value, true);
        try
        {
            // Windows' own number: a reinstall changes it, so it is kept but never used to say "same PC".
            Add("machine", Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)?.ToString(), false);
        }
        catch
        {
            // Not readable: skipped.
        }
        if (parts.Count == 0) return null;
        // One value for all the stable parts together, when there are at least two of them to make it worth anything.
        if (stable.Count >= 2) parts.Insert(0, new HwidPart { Kind = "id", Hash = Hash("id", string.Join("|", stable.OrderBy(hash => hash, StringComparer.Ordinal))) });
        return new HwidInfo { Parts = parts.Take(12).ToList() };
    }
}
