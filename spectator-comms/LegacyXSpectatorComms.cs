using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace LegacyXSpectatorComms;

public sealed class LegacyXSpectatorCommsConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public bool EnforceTextIsolation { get; set; } = true;
    public bool EnforceCompetitiveVoiceCvars { get; set; } = true;
    public bool BlockLivingGlobalChat { get; set; } = true;
    public bool PreservePluginCommands { get; set; } = true;
    public string ChatPrefix { get; set; } = "{Lime}[LEGACY-X]{Default}";
}

public sealed class LegacyXSpectatorComms : BasePlugin, IPluginConfig<LegacyXSpectatorCommsConfig>
{
    public required LegacyXSpectatorCommsConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Spectator Comms";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXSpectatorCommsConfig config) => Config = config;

    public override void Load(bool hotReload)
    {
        AddCommandListener("say", OnSay, HookMode.Pre);
        AddCommandListener("say_team", OnSay, HookMode.Pre);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        ApplyVoiceBaseline();
        Console.WriteLine($"[{ModuleName}] Loaded — spectator text isolation and competitive voice baseline enabled.");
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
        player.PrintToChat($"{Config.ChatPrefix} Spectators can text-chat only with the spectator/dead channel. Do not relay live information.");
    }

    private HookResult OnSay(CCSPlayerController? sender, CommandInfo command)
    {
        if (!Config.Enabled || !Config.EnforceTextIsolation || sender == null || !sender.IsValid || sender.IsBot || sender.IsHLTV) return HookResult.Continue;

        var message = Normalize(command.ArgString);
        if (string.IsNullOrWhiteSpace(message)) return HookResult.Handled;
        if (Config.PreservePluginCommands && (message.StartsWith("!") || message.StartsWith("."))) return HookResult.Continue;

        var senderChannel = GetChannel(sender);
        foreach (var recipient in Utilities.GetPlayers())
        {
            if (!CanReceive(recipient, senderChannel)) continue;
            recipient.PrintToChat(Format(sender, senderChannel, message));
        }
        return HookResult.Handled;
    }

    private void ApplyVoiceBaseline()
    {
        if (!Config.Enabled || !Config.EnforceCompetitiveVoiceCvars) return;
        Server.ExecuteCommand("sv_alltalk 0");
        Server.ExecuteCommand("sv_full_alltalk 0");
        Server.ExecuteCommand("sv_deadtalk 0");
    }

    private bool CanReceive(CCSPlayerController? recipient, ChatChannel senderChannel)
    {
        if (recipient == null || !recipient.IsValid || recipient.IsBot || recipient.IsHLTV) return false;
        var recipientChannel = GetChannel(recipient);
        if (senderChannel == ChatChannel.Spectator) return recipientChannel == ChatChannel.Spectator;
        return recipientChannel == senderChannel;
    }

    private ChatChannel GetChannel(CCSPlayerController player)
    {
        if (!IsAliveCompetitive(player)) return ChatChannel.Spectator;
        return player.Team == CsTeam.Terrorist ? ChatChannel.Terrorist : ChatChannel.CounterTerrorist;
    }

    private static bool IsAliveCompetitive(CCSPlayerController player)
    {
        return player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist
            && player.PlayerPawn.IsValid
            && player.PlayerPawn.Value?.LifeState == (byte)LifeState_t.LIFE_ALIVE;
    }

    private string Format(CCSPlayerController sender, ChatChannel channel, string message)
    {
        var label = channel switch
        {
            ChatChannel.Spectator => "{Grey}[SPEC]",
            ChatChannel.Terrorist => "{Red}[T]",
            _ => "{Blue}[CT]",
        };
        return $"{Config.ChatPrefix} {label} {{Default}}{sender.PlayerName}: {message}";
    }

    private static string Normalize(string raw)
    {
        var clean = (raw ?? string.Empty).Replace("\"", string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 192 ? clean : clean[..192];
    }

    private enum ChatChannel { Spectator, Terrorist, CounterTerrorist }
}
