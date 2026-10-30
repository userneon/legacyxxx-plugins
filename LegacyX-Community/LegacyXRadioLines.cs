using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
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
}
