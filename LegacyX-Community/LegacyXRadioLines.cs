using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.UserMessages;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

/// <summary>
/// The game's grenade radio calls ("Name @ Place (RADIO): Smoke!", "Fire in the hole!", "Flashbang!") are shown in the LEGACY-X chat look
/// instead (owner request 2026-10-30): "RADIO | Name: Smoke!", to the same players who would have got the game's line. RadioLines:
/// "prefix" (default), "hide" (no line) or "game" (the game's own line). Only the grenade calls are changed; other radio calls stay as they are.
/// The first time an unknown radio line is seen its text is written to the server log, so the list below can grow.
/// </summary>
public sealed partial class LegacyXCommunity
{
    private readonly HashSet<string> radioSeen = new();

    private static readonly (string Token, string Text)[] RadioCalls =
    {
        ("Smoke_in_the_hole", "Smoke!"), ("Flashbang_in_the_hole", "Flashbang!"), ("Fire_in_the_hole", "Fire in the hole!"),
        ("Molotov_in_the_hole", "Molotov!"), ("Decoy_in_the_hole", "Decoy!"), ("Incendiary_in_the_hole", "Incendiary!"),
    };

    private const int SayText2Id = 118; // UM_SayText2: where CS2 sends the radio call line ("#Game_radio_location": name, place, text).

    private void RegisterRadioLines()
    {
        if (string.Equals(Config.RadioLines, "game", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(Config.RadioLines, "debug", StringComparison.OrdinalIgnoreCase))
        {
            // For finding a message by hand: every kind of message the game sends is written to the log once, with its number and its fields.
            for (var id = 100; id < 420; id++)
            {
                var number = id;
                try
                {
                    HookUserMessage(number, message =>
                    {
                        if (radioSeen.Count < 400 && radioSeen.Add("um:" + number))
                        {
                            string text;
                            try { text = string.Join(" | ", new[] { "messagename", "msg_name", "param1", "param2", "param3", "params" }.Select(f => { try { return f + "=" + message.ReadString(f); } catch { return ""; } }).Where(x => x.Length > 0)); }
                            catch { text = ""; }
                            Console.WriteLine($"[{ModuleName}] UserMessage {number}: {text}");
                        }
                        return HookResult.Continue;
                    }, HookMode.Pre);
                }
                catch { }
            }
            Console.WriteLine($"[{ModuleName}] Radio lines: debug, every kind of message is written to the log once.");
            return;
        }
        // The game's own grenade call is switched off (sv_ignoregrenaderadio) and the same call is written in the LEGACY-X look when the grenade is thrown,
        // to the thrower's team: this does not depend on which message the game sends the call in.
        // Set at every map start, never while the plugin loads: the game is not up yet then.
        RegisterListener<Listeners.OnMapStart>(_ => SetIgnoreGrenadeRadio());
        if (!string.Equals(Config.RadioLines, "hide", StringComparison.OrdinalIgnoreCase))
        {
            RegisterEventHandler<EventWeaponFire>((@event, _) =>
            {
                try
                {
                    var call = @event.Weapon switch
                    {
                        "weapon_smokegrenade" => "Smoke!", "weapon_flashbang" => "Flashbang!", "weapon_hegrenade" => "Fire in the hole!",
                        "weapon_molotov" => "Molotov!", "weapon_incgrenade" => "Incendiary!", "weapon_decoy" => "Decoy!", _ => null,
                    };
                    if (call is null || @event.Userid is not { IsValid: true } thrower || thrower.IsBot) return HookResult.Continue;
                    var line = LegacyXChat.Radio(thrower.PlayerName, call, thrower.TeamNum);
                    foreach (var other in Utilities.GetPlayers().Where(o => o.IsValid && !o.IsBot && !o.IsHLTV && o.TeamNum == thrower.TeamNum)) other.PrintToChat(line);
                }
                catch { }
                return HookResult.Continue;
            });
        }
        HookUserMessage(SayText2Id, OnRadioSayText, HookMode.Pre);
        Console.WriteLine($"[{ModuleName}] Radio lines are shown as '{Config.RadioLines}' (SayText2 hook).");
        try
        {
            var id = UserMessage.FromPartialName("RadioText").Id;
            HookUserMessage(id, OnRadioLine, HookMode.Pre);
            Console.WriteLine($"[{ModuleName}] Radio lines are shown as '{Config.RadioLines}' (message {id}).");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] Radio lines stay as the game shows them: {exception.Message}");
        }
    }

    private HookResult OnRadioLine(UserMessage message)
    {
        try
        {
            var count = message.GetRepeatedFieldCount("params");
            var parts = new List<string>();
            for (var index = 0; index < count; index++) parts.Add(message.ReadString("params", index) ?? "");
            var call = RadioCalls.FirstOrDefault(entry => parts.Any(part => part.Contains(entry.Token, StringComparison.OrdinalIgnoreCase)));
            if (call.Token == null)
            {
                var seen = string.Join(" | ", new[] { message.ReadString("msg_name") ?? "" }.Concat(parts));
                if (radioSeen.Add(seen)) Console.WriteLine($"[{ModuleName}] Radio line left as the game shows it: {seen}");
                return HookResult.Continue;
            }
            if (string.Equals(Config.RadioLines, "hide", StringComparison.OrdinalIgnoreCase)) return HookResult.Handled;
            var thrower = Utilities.GetPlayerFromIndex(message.ReadInt("client"));
            var name = parts.Count > 0 && !parts[0].StartsWith('#') ? parts[0] : thrower?.PlayerName ?? "";
            var line = LegacyXChat.Radio(name, call.Text, thrower?.TeamNum ?? 0);
            foreach (var player in message.Recipients)
            {
                if (player is CCSPlayerController controller && controller.IsValid && !controller.IsBot) controller.PrintToChat(line);
            }
            return HookResult.Handled;
        }
        catch
        {
            return HookResult.Continue;
        }
    }

    private static readonly (string Key, string Text)[] RadioWords =
    {
        ("smoke", "Smoke!"), ("flashbang", "Flashbang!"), ("fireinthehole", "Fire in the hole!"),
        ("molotov", "Molotov!"), ("decoy", "Decoy!"), ("incendiary", "Incendiary!"),
    };

    /// <summary>CS2 prints a radio call as a SayText2 line whose name contains "radio"; the plain player chat shares the message and is never touched.</summary>
    private HookResult OnRadioSayText(UserMessage message)
    {
        try
        {
            var name = message.ReadString("messagename") ?? "";
            if (name.IndexOf("radio", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Plain player chat is "Cstrike_Chat_*"; any other kind of line is written to the log once, so the radio call can be named from it.
                if (!name.StartsWith("Cstrike_Chat", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("#Cstrike_Chat", StringComparison.OrdinalIgnoreCase)
                    && radioSeen.Count < 100 && radioSeen.Add("say:" + name))
                    Console.WriteLine($"[{ModuleName}] Chat line kind: {name} | {message.ReadString("param1")} | {message.ReadString("param2")} | {message.ReadString("param3")} | {message.ReadString("param4")}");
                return HookResult.Continue;
            }
            var parts = new List<string>();
            for (var index = 1; index <= 4; index++) parts.Add(message.ReadString("param" + index) ?? "");
            var flat = new string(string.Concat(parts).Where(char.IsLetter).ToArray()).ToLowerInvariant();
            var call = RadioWords.FirstOrDefault(entry => flat.Contains(entry.Key));
            if (call.Key == null)
            {
                var seen = name + " | " + string.Join(" | ", parts);
                if (radioSeen.Add(seen)) Console.WriteLine($"[{ModuleName}] Radio line left as the game shows it: {seen}");
                return HookResult.Continue;
            }
            if (string.Equals(Config.RadioLines, "hide", StringComparison.OrdinalIgnoreCase)) return HookResult.Handled;
            var thrower = Utilities.GetPlayerFromIndex(message.ReadInt("entityindex"));
            var who = parts[0].Length > 0 && !parts[0].StartsWith('#') ? parts[0] : thrower?.PlayerName ?? "";
            var line = LegacyXChat.Radio(who, call.Text, thrower?.TeamNum ?? 0);
            foreach (var player in message.Recipients)
            {
                if (player is CCSPlayerController controller && controller.IsValid && !controller.IsBot) controller.PrintToChat(line);
            }
            return HookResult.Handled;
        }
        catch (Exception exception)
        {
            if (radioSeen.Add("error " + exception.Message)) Console.WriteLine($"[{ModuleName}] Radio line hook error: {exception.Message}");
            return HookResult.Continue;
        }
    }

    private void SetIgnoreGrenadeRadio()
    {
        try
        {
            var convar = ConVar.Find("sv_ignoregrenaderadio");
            if (convar == null) { Console.WriteLine($"[{ModuleName}] sv_ignoregrenaderadio does not exist on this game version: the game's own grenade call stays."); return; }
            convar.SetValue(true);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[{ModuleName}] sv_ignoregrenaderadio could not be set: {exception.Message}");
        }
    }
}
