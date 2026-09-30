using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;
using PanoramaManager;

namespace LegacyXHud;

/// <summary>
/// Shows the LEGACY-X Workshop layouts (legacyxxx-workshop) to players. The layout is drawn on the
/// client (Workshop addon via MultiAddonManager); this plugin creates the custom_hud_layout entity and
/// fills it per player: texts by Label id, states by toggling classes (see CONTRACT.md of the addon).
///
/// First version = a pipeline test: a welcome announcement (legacyx_notify "ann") when a player joins,
/// and !lxhud to show it again. Rank card, match card, knife vote and admin panel come after this works.
/// </summary>
public sealed class LegacyXHud : BasePlugin
{
    // The source path of the layout, extension included (CONTRACT.md). If nothing shows in game, try
    // LEGACYX_HUD_NOTIFY_LAYOUT=panorama/layout/custom_game/legacyx_notify.vxml_c in the .env:
    // PanoramaManager's own example uses the compiled name.
    private const string DefaultNotifyLayout = "panorama/layout/custom_game/legacyx_notify.xml";

    private bool enabled;
    private string notifyLayout = DefaultNotifyLayout;
    private PanelHandle? notify;

    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Hud";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public override void Load(bool hotReload)
    {
        var env = LegacyXEnvironmentLoader.Load();
        enabled = env.GetModuleBoolean("HUD", "ENABLED", true);
        if (!enabled)
        {
            Console.WriteLine($"[{ModuleName}] Disabled by central environment.");
            return;
        }
        notifyLayout = env.Get("LEGACYX_HUD_NOTIFY_LAYOUT", DefaultNotifyLayout);

        Panorama.Init(this);

        // The entity system is not ready at plugin load: spawn after the first round starts.
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        Console.WriteLine($"[{ModuleName}] Ready. Layout {notifyLayout}");
    }

    public override void Unload(bool hotReload)
    {
        notify?.Dispose();
        notify = null;
        if (enabled) Panorama.Shutdown();
    }

    private HookResult OnRoundStart(EventRoundStart e, GameEventInfo info)
    {
        EnsureNotify();
        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull e, GameEventInfo info)
    {
        var player = e.Userid;
        if (player is null || !player.IsValid || player.IsBot) return HookResult.Continue;
        var slot = player.Slot;
        // A few seconds: the client must finish loading the map and the addon before it can draw the panel.
        AddTimer(5f, () =>
        {
            var joined = Utilities.GetPlayerFromSlot(slot);
            if (joined is { IsValid: true, IsBot: false })
                Announce(joined, $"Welcome to LEGACY-X, {joined.PlayerName}", "Custom HUD is on. Type !lxhud to test it again.");
        });
        return HookResult.Continue;
    }

    [ConsoleCommand("css_lxhud", "Show the LEGACY-X HUD test announcement to yourself")]
    public void OnLxHud(CCSPlayerController? player, CommandInfo command)
    {
        if (!enabled || player is not { IsValid: true }) return;
        Announce(player, "LEGACY-X HUD test", "If you can read this, the Workshop addon and the plugin work together.");
    }

    private PanelHandle? EnsureNotify()
    {
        if (notify is not null) return notify;
        try
        {
            notify = Panorama.Spawn(notifyLayout);
            notify.CaptureInput = false; // read-only panel: never pull up a cursor
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{ModuleName}] Could not spawn {notifyLayout}: {ex.Message}");
            notify = null;
        }
        return notify;
    }

    /// <summary>legacyx_notify "ann": sets the two texts for this player, drops the banner in, then away.</summary>
    private void Announce(CCSPlayerController player, string title, string body)
    {
        var panel = EnsureNotify();
        if (panel is null) return;
        panel.SetVariableFor(player, "ann_title", title);
        panel.SetVariableFor(player, "ann_body", body);
        panel.SetClassFor(player, "ann", "open", true);

        var slot = player.Slot;
        AddTimer(7f, () =>
        {
            var still = Utilities.GetPlayerFromSlot(slot);
            if (still is { IsValid: true } && notify is not null)
                notify.SetClassFor(still, "ann", "open", false);
        });
    }
}
