using System.Linq;
using LegacyX.Admin.Authorization;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

public class StaffPermissionsTests
{
    [Theory]
    [InlineData("owner", StaffRole.Owner)]
    [InlineData("manager", StaffRole.Manager)]
    [InlineData("admin", StaffRole.Admin)]
    [InlineData("staff", StaffRole.Staff)]
    [InlineData("creator", StaffRole.Creator)]
    [InlineData("main_creator", StaffRole.MainCreator)]
    [InlineData("player", StaffRole.Player)]
    public void Parses_the_api_role_names(string value, StaffRole expected)
    {
        Assert.True(StaffPermissions.TryParseRole(value, out var role));
        Assert.Equal(expected, role);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("OWNER")]
    [InlineData("root")]
    [InlineData("moderator")]
    [InlineData("")]
    [InlineData(" admin")]
    [InlineData(null)]
    public void Rejects_anything_else_as_player(string? value)
    {
        Assert.False(StaffPermissions.TryParseRole(value, out var role));
        Assert.Equal(StaffRole.Player, role);
    }

    [Fact]
    public void Player_gets_nothing()
    {
        var grant = StaffPermissions.For(StaffRole.Player);
        Assert.Empty(grant.Flags);
        Assert.Equal(0u, grant.Immunity);
        Assert.Equal(0, grant.Stamina);
    }

    [Fact]
    public void Only_owner_gets_root_rcon_cvar_and_cheats()
    {
        foreach (var role in new[] { StaffRole.Staff, StaffRole.Admin, StaffRole.Manager })
            foreach (var flag in new[] { "@css/root", "@css/rcon", "@css/cvar", "@css/cheats" })
                Assert.DoesNotContain(flag, StaffPermissions.For(role).Flags);
        Assert.Contains("@css/root", StaffPermissions.For(StaffRole.Owner).Flags);
    }

    [Fact]
    public void Each_role_includes_everything_of_the_role_below_and_ranks_higher()
    {
        var ordered = new[] { StaffRole.Player, StaffRole.Staff, StaffRole.Admin, StaffRole.Manager, StaffRole.Owner };
        for (var i = 1; i < ordered.Length; i++)
        {
            var lower = StaffPermissions.For(ordered[i - 1]);
            var higher = StaffPermissions.For(ordered[i]);
            Assert.All(lower.Flags, flag => Assert.Contains(flag, higher.Flags));
            Assert.True(higher.Immunity > lower.Immunity);
            Assert.True(higher.Stamina > lower.Stamina);
        }
    }

    [Theory]
    [InlineData(StaffRole.Staff, "@css/ban", false)]
    [InlineData(StaffRole.Staff, "@css/chat", true)]
    [InlineData(StaffRole.Admin, "@css/ban", true)]
    [InlineData(StaffRole.Admin, "@css/unban", false)]
    [InlineData(StaffRole.Admin, "@css/config", false)]
    [InlineData(StaffRole.Staff, "@legacyx/match", false)]
    [InlineData(StaffRole.Admin, "@legacyx/match", true)]
    [InlineData(StaffRole.Manager, "@css/unban", true)]
    [InlineData(StaffRole.Manager, "@css/config", true)]
    public void Maps_role_to_flags(StaffRole role, string flag, bool expected)
    {
        Assert.Equal(expected, StaffPermissions.For(role).Flags.Contains(flag));
    }

    [Fact]
    public void Creators_get_the_fun_commands_and_nothing_that_punishes_or_runs_the_server()
    {
        foreach (var role in new[] { StaffRole.Creator, StaffRole.MainCreator })
        {
            var flags = StaffPermissions.For(role).Flags;
            Assert.Contains("@css/slay", flags);
            Assert.Contains("@css/cheats", flags);
            foreach (var flag in new[] { "@css/generic", "@css/kick", "@css/ban", "@css/unban", "@css/chat", "@css/vote", "@css/changemap", "@css/config", "@css/cvar", "@css/rcon", "@css/root", "@legacyx/match" })
                Assert.DoesNotContain(flag, flags);
        }
    }

    [Fact]
    public void A_main_creator_outranks_a_creator_and_staff_outrank_both()
    {
        var creator = StaffPermissions.For(StaffRole.Creator);
        var main = StaffPermissions.For(StaffRole.MainCreator);
        var staff = StaffPermissions.For(StaffRole.Staff);
        Assert.True(main.Immunity > creator.Immunity);
        Assert.True(staff.Immunity > main.Immunity);
        Assert.True(StaffRole.Staff > StaffRole.MainCreator && StaffRole.MainCreator > StaffRole.Creator);
    }
}
