using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegacyXAntiWallhack;
using Xunit;

namespace LegacyX.Admin.Authorization.Tests;

public class AntiWallhackTests
{
    [Fact]
    public void Samples_the_body_and_a_padded_box_round_it()
    {
        var points = new List<Vector3>();
        AntiWallhackLogic.SamplePoints(new Vector3(100, 200, 0), ducking: false, padding: 28, points);
        Assert.Equal(11, points.Count);                                   // 3 on the body, 4 corners at 2 heights
        Assert.Contains(new Vector3(100, 200, 60), points);               // the head
        Assert.Contains(new Vector3(100 + 44, 200 + 44, 8), points);      // 16 (half the width) + 28 (the padding)
        Assert.All(points, point => Assert.InRange(point.Z, 8, 60));
    }

    [Fact]
    public void A_crouching_enemy_is_lower()
    {
        var standing = new List<Vector3>(); var crouching = new List<Vector3>();
        AntiWallhackLogic.SamplePoints(Vector3.Zero, false, 0, standing);
        AntiWallhackLogic.SamplePoints(Vector3.Zero, true, 0, crouching);
        Assert.True(crouching.Max(p => p.Z) < standing.Max(p => p.Z));
    }

    [Fact]
    public void Smoke_blocks_a_line_that_passes_through_it_and_not_one_that_passes_beside_it()
    {
        var center = new Vector3(500, 0, 0);
        Assert.True(AntiWallhackLogic.SegmentCrossesSphere(Vector3.Zero, new Vector3(1000, 0, 0), center, 125));
        Assert.True(AntiWallhackLogic.SegmentCrossesSphere(Vector3.Zero, new Vector3(1000, 100, 0), center, 125));
        Assert.False(AntiWallhackLogic.SegmentCrossesSphere(Vector3.Zero, new Vector3(1000, 400, 0), center, 125));
        Assert.False(AntiWallhackLogic.SegmentCrossesSphere(Vector3.Zero, new Vector3(300, 0, 0), center, 125));   // stops short of it
    }

    [Fact]
    public void An_enemy_is_hidden_only_after_the_grace_and_shown_at_once_when_seen()
    {
        var state = new AntiWallhackLogic.PairState { LastVisibleTick = 100 };
        Assert.False(AntiWallhackLogic.Update(state, seen: false, tick: 110, graceTicks: 20));   // still within the grace
        Assert.False(AntiWallhackLogic.Update(state, seen: false, tick: 120, graceTicks: 20));
        Assert.True(AntiWallhackLogic.Update(state, seen: false, tick: 121, graceTicks: 20));
        Assert.True(AntiWallhackLogic.Update(state, seen: false, tick: 300, graceTicks: 20));
        Assert.False(AntiWallhackLogic.Update(state, seen: true, tick: 301, graceTicks: 20));    // seen again: shown at once
        Assert.False(AntiWallhackLogic.Update(state, seen: false, tick: 310, graceTicks: 20));   // and the grace starts over
    }

    [Theory]
    [InlineData("competitive_5v5", "competitive_5v5,proleague", true)]
    [InlineData("competitive_5v5_2", "competitive_5v5,proleague", true)]
    [InlineData("proleague", "competitive_5v5, proleague", true)]
    [InlineData("fun", "competitive_5v5,proleague", false)]
    [InlineData("", "competitive_5v5", false)]
    public void Runs_only_on_the_listed_modes(string mode, string list, bool expected) => Assert.Equal(expected, AntiWallhackLogic.ModeMatches(mode, list));
}
