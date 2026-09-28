using System;
using System.IO;
using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>The .env is found from where CS2 actually runs plugins (AMP layout, lowercase folder, .env.txt).</summary>
public class EnvironmentFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "legacyx-env-" + Guid.NewGuid().ToString("N"));
    private string Css => Path.Combine(root, "game", "csgo", "addons", "counterstrikesharp");

    public EnvironmentFileTests()
    {
        Directory.CreateDirectory(Path.Combine(Css, "shared", "LegacyX.Shared.Configuration"));
        Directory.CreateDirectory(Path.Combine(root, "game", "bin", "linuxsteamrt64"));
        File.WriteAllText(Path.Combine(Css, ".env.txt"), "LEGACYX_API_BASE_URL=https://api.example\n");
    }

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public void FoundFromTheSharedLibraryFolder() =>
        Assert.Equal(Path.Combine(Css, ".env.txt"), LegacyXEnvironmentLoader.FindEnvironmentFile(new[] { Path.Combine(Css, "shared", "LegacyX.Shared.Configuration") }));

    [Fact]
    public void FoundFromTheGameBinFolder() =>
        Assert.Equal(Path.Combine(Css, ".env.txt"), LegacyXEnvironmentLoader.FindEnvironmentFile(new[] { Path.Combine(root, "game", "bin", "linuxsteamrt64") }));

    [Fact]
    public void EnvWinsOverEnvTxt()
    {
        File.WriteAllText(Path.Combine(Css, ".env"), "");
        Assert.Equal(Path.Combine(Css, ".env"), LegacyXEnvironmentLoader.FindEnvironmentFile(new[] { Css }));
    }
}
