using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using PanoramaManager;

namespace LegacyXHud;

// The match intro (owner request 2026-11-01): when a match goes live (legacyx_match_live turns on) every player sees the LEGACY-X logo, the name and "MATCH START"
// in the middle of the screen for a few seconds. Layout legacyx_intro.xml; the fade and the line are CSS transitions started by classes.
public sealed partial class LegacyXHud
{
    private const float IntroShownSeconds = 3.5f;
    private const float IntroFadeSeconds = 0.8f;

    private string introLayout = LayoutDir + "legacyx_intro.vxml_c";
    private PanelHandle? introHud;
    private bool matchWasLive;

    private void RegisterIntro() => AddTimer(0.5f, IntroTick, TimerFlags.REPEAT);

    private void IntroTick()
    {
        bool live;
        try { live = ConVar.Find("legacyx_match_live")?.GetPrimitiveValue<bool>() == true; }
        catch { live = false; }
        var started = live && !matchWasLive;
        matchWasLive = live;
        if (started) ShowIntro();
    }

    private void ShowIntro()
    {
        var panel = Ensure(ref introHud, introLayout, "lx_intro");
        if (panel is null) return;
        var shown = new List<(int Slot, ulong SteamId)>();
        foreach (var player in Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }))
        {
            if (!OpenFor(panel, player)) continue;
            panel.SetClassFor(player, "intro", "shown", true);
            panel.SetClassFor(player, "intro_line", "go", true);
            shown.Add((player.Slot, player.SteamID));
        }
        Console.WriteLine($"[{ModuleName}] The match intro is shown to {shown.Count} player(s).");
        AddTimer(IntroShownSeconds, () =>
        {
            foreach (var (slot, steamId) in shown)
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player is not { IsValid: true } || player.SteamID != steamId || introHud is null || !introHud.IsOpenFor(player)) continue;
                introHud.SetClassFor(player, "intro", "shown", false);
                introHud.SetClassFor(player, "intro_line", "go", false);
            }
            // Closed after the fade has finished.
            AddTimer(IntroFadeSeconds, () =>
            {
                foreach (var (slot, steamId) in shown)
                {
                    var player = Utilities.GetPlayerFromSlot(slot);
                    if (player is { IsValid: true } && player.SteamID == steamId && introHud is { } handle && handle.IsOpenFor(player)) handle.Close(player);
                }
            });
        });
    }
}
