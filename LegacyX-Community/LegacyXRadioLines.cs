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

    private void RegisterRadioLines()
    {
        if (string.Equals(Config.RadioLines, "game", StringComparison.OrdinalIgnoreCase)) return;
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
}
