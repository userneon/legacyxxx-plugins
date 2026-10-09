using System.Windows;

namespace LegacyX.Checker;

public partial class App : Application
{
    public const string Version = "1.0.0";

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
