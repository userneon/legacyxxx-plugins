using System.Windows;

namespace LegacyX.Checker;

public partial class App : Application
{
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
