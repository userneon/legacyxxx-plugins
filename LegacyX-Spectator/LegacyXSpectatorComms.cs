using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;

namespace LegacyXSpectatorComms;

public sealed class LegacyXSpectatorCommsConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnforceTextIsolation { get; set; } = true;
    public bool EnforceCompetitiveVoiceCvars { get; set; } = true;
    public bool BlockLivingGlobalChat { get; set; } = true;
    public bool PreservePluginCommands { get; set; } = true;
    public string ChatPrefix { get; set; } = LegacyXChat.Prefix;
}

public sealed class LegacyXSpectatorComms : BasePlugin, IPluginConfig<LegacyXSpectatorCommsConfig>
{
    public required LegacyXSpectatorCommsConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Spectator";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXSpectatorCommsConfig config)
    {
        config.Enabled = LegacyXEnvironmentLoader.Load().GetModuleBoolean("SPECTATOR_COMMS", "ENABLED", config.Enabled);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        // Chat text is formatted and routed by LegacyX-Admin (ALL | Name: text, TEAM | Name: text; the dead only reach the dead). This plugin used
        // to reroute it too, so every line was sent twice, once as "(CT) Name: text" with no sign of all or team chat.
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        ApplyVoiceBaseline();
        Console.WriteLine($"[{ModuleName}] Loaded — competitive voice baseline enabled (chat is handled by LegacyX-Admin).");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        ApplyVoiceBaseline();
        return HookResult.Continue;
    }

    [ConsoleCommand("css_specrules", "Shows LEGACY-X spectator communication rules")]
    public void OnRules(CCSPlayerController? player, CommandInfo? command)
    {
        if (player == null || !player.IsValid) return;
        player.PrintToChat(LegacyXChat.System("Spectators and dead players only chat with each other."));
    }

    private void ApplyVoiceBaseline()
    {
        if (!Config.Enabled || !Config.EnforceCompetitiveVoiceCvars) return;
        // sv_alltalk / sv_full_alltalk belong to LegacyX-MatchZy now (everyone talks in warmup, the knife vote and the
        // halftime swap, teams only in the knife round and the live match); setting them here every round would undo that.
        // Owner request: living players hear their dead teammates (sv_deadtalk 1); enemies stay unheard.
        Server.ExecuteCommand("sv_deadtalk 1");
    }
}
