namespace LegacyX.Checker;

/// <summary>Paths are shortened before they leave the PC: the user name inside C:\Users\name is hidden.</summary>
public static class Masking
{
    public static string Path(string path)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var masked = path;
        if (!string.IsNullOrEmpty(profile) && path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
        {
            masked = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(profile) ?? "C:\\Users", "***") + path[profile.Length..];
        }
        return Trim(masked, 200);
    }

    public static string Trim(string text, int max) => text.Length <= max ? text : text[..max];
}
