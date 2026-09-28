using System;
using System.Collections.Generic;
using LegacyX.Shared.Configuration;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>Several CS2 servers sharing one install and one .env, told apart by port.</summary>
public class EnvironmentTests
{
    private static readonly Dictionary<string, string> SharedEnv = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LEGACYX_SERVER_HOST"] = "203.0.113.5",
        ["LEGACYX_SERVER_MODE"] = "competitive_5v5",
        ["LEGACYX_27016_SERVER_MODE"] = "fun",
        ["LEGACYX_27016_SERVER_NAME"] = "Fun #1",
        ["LEGACYX_PLUGIN_TOKEN"] = "shared-token",
    };

    [Fact]
    public void Each_port_gets_its_own_identity_from_the_shared_file()
    {
        var first = new LegacyXEnvironment(SharedEnv, ".env", 27015);
        var second = new LegacyXEnvironment(SharedEnv, ".env", 27016);

        Assert.Equal("srv-27015", first.Get("LEGACYX_SERVER_ID"));
        Assert.Equal("203.0.113.5:27015", first.Get("LEGACYX_SERVER_ADDRESS"));
        Assert.Equal("competitive_5v5", first.Get("LEGACYX_SERVER_MODE"));
        Assert.Equal("", first.Get("LEGACYX_SERVER_NAME"));

        Assert.Equal("srv-27016", second.Get("LEGACYX_SERVER_ID"));
        Assert.Equal("203.0.113.5:27016", second.Get("LEGACYX_SERVER_ADDRESS"));
        Assert.Equal("fun", second.Get("LEGACYX_SERVER_MODE"));
        Assert.Equal("Fun #1", second.Get("LEGACYX_SERVER_NAME"));

        Assert.Equal("shared-token", second.GetModule("STATUS", "PLUGIN_TOKEN"));
    }

    [Fact]
    public void An_explicit_value_still_wins_and_no_port_means_no_guessing()
    {
        var explicitId = new Dictionary<string, string>(SharedEnv, StringComparer.OrdinalIgnoreCase) { ["LEGACYX_SERVER_ID"] = "main" };
        Assert.Equal("main", new LegacyXEnvironment(explicitId, ".env", 27015).Get("LEGACYX_SERVER_ID"));

        var prefixed = new Dictionary<string, string>(SharedEnv, StringComparer.OrdinalIgnoreCase) { ["LEGACYX_SERVER_ID_PREFIX"] = "eu" };
        Assert.Equal("eu-27015", new LegacyXEnvironment(prefixed, ".env", 27015).Get("LEGACYX_SERVER_ID"));

        var unknownPort = new LegacyXEnvironment(SharedEnv, ".env", null);
        Assert.Equal("", unknownPort.Get("LEGACYX_SERVER_ID"));
        Assert.Equal("fallback", unknownPort.Get("LEGACYX_SERVER_ADDRESS", "fallback"));
    }

    [Theory]
    [InlineData(new[] { "cs2", "-dedicated", "-port", "27016", "+map", "de_dust2" }, null, 27016)]
    [InlineData(new[] { "cs2", "-port=27017" }, null, 27017)]
    [InlineData(new[] { "cs2", "+hostport", "27018" }, null, 27018)]
    [InlineData(new[] { "cs2", "-port", "27016" }, "27020", 27020)]
    [InlineData(new[] { "cs2", "-dedicated" }, null, null)]
    [InlineData(new[] { "cs2", "-port", "notaport" }, "", null)]
    public void Finds_the_port_on_the_command_line(string[] commandLine, string? variable, int? expected)
    {
        Assert.Equal(expected, LegacyXEnvironment.DetectPort(commandLine, variable));
    }
}
