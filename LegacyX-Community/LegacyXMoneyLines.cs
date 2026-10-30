using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.UserMessages;
using LegacyX.Shared.Configuration;

namespace LegacyXCommunity;

/// <summary>
/// The game's money lines in chat ("+$300: Award for neutralizing an enemy", "+$3250: Team award for winning…") are replaced by one
/// short line in the LEGACY-X style (owner request 2026-10-25): "LEGACY-X | +$300 · Kill". The money itself is untouched (no
/// mp_*cashawards change); only the text is. The amount comes from the game's own message. MoneyLines: "prefix" (default),
/// "hide" (no line at all) or "game" (the game's own line).
/// </summary>
public sealed partial class LegacyXCommunity
{
    private readonly HashSet<string> textSeen = new();
    private const int TextMsgId = 124; // UM_TextMsg: the game's localized chat and centre lines.

    private void RegisterMoneyLines()
    {
        if (string.Equals(Config.MoneyLines, "game", StringComparison.OrdinalIgnoreCase)) return;
        HookUserMessage(TextMsgId, OnMoneyLine, HookMode.Pre);
    }

    private HookResult OnMoneyLine(UserMessage message)
    {
        try
        {
            var token = message.ReadString("param", 0);
            // "<name> attacked a teammate" (the game's warning line, only utility can hurt a teammate now): the same words in the LEGACY-X look.
            if (!string.IsNullOrEmpty(token) && (token.Contains("teammate_attack", StringComparison.OrdinalIgnoreCase) || token.Contains("attacked_teammate", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.Equals(Config.MoneyLines, "game", StringComparison.OrdinalIgnoreCase)) return HookResult.Continue;
                var who = message.GetRepeatedFieldCount("param") > 1 ? CleanName(ReadSlotName(message.ReadString("param", 1), null).Name) : "";
                var warning = LegacyXChat.System(who.Length > 0 ? $"{{white}}{who}{{grey}} hurt a teammate." : "A teammate was hurt.");
                foreach (var player in message.Recipients)
                {
                    if (player is CCSPlayerController controller && controller.IsValid && !controller.IsBot) controller.PrintToChat(warning);
                }
                return HookResult.Handled;
            }
            var label = MoneyLabel(token);
            if (label == null)
            {
                // A line the game shows on its own (not money): its text token is written to the log once, so every default message can be listed.
                var other = string.Join(" | ", Enumerable.Range(0, Math.Min(message.GetRepeatedFieldCount("param"), 4)).Select(index => message.ReadString("param", index) ?? ""));
                if (!string.IsNullOrWhiteSpace(other) && textSeen.Count < 200 && textSeen.Add(other)) Console.WriteLine($"[{ModuleName}] Game message: {other}");
                return HookResult.Continue;
            }
            if (string.Equals(Config.MoneyLines, "hide", StringComparison.OrdinalIgnoreCase)) return HookResult.Handled;
            var amount = message.GetRepeatedFieldCount("param") > 1 ? message.ReadString("param", 1) : "";
            var line = LegacyXChat.System(MoneyText(amount, label));
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

    /// <summary>"+$300 · Kill" in green, a loss ("-$300 · Teamkill") in white; just the label when the game sent no amount.</summary>
    internal static string MoneyText(string rawAmount, string label)
    {
        var digits = Regex.Match(rawAmount ?? "", @"\d[\d,]*").Value.Replace(",", "");
        if (digits.Length == 0) return label;
        var negative = (rawAmount ?? "").Contains('-');
        return negative ? $"{{white}}-${digits}{{grey}} · {label}" : $"{{green}}+${digits}{{grey}} · {label}";
    }

    /// <summary>A short name for a money message, or null when the message is not about money.</summary>
    internal static string? MoneyLabel(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var player = token.StartsWith("#Player_Cash_Award", StringComparison.OrdinalIgnoreCase);
        var team = token.StartsWith("#Team_Cash_Award", StringComparison.OrdinalIgnoreCase);
        if (!player && !team) return null;
        bool Has(string part) => token.Contains(part, StringComparison.OrdinalIgnoreCase);
        if (player)
        {
            if (Has("Kill_Teammate")) return "Teamkill";
            if (Has("Killed_Enemy")) return "Kill";
            if (Has("Bomb_Planted")) return "Bomb planted";
            if (Has("Bomb_Defused")) return "Bomb defused";
            if (Has("Rescued_Hostage")) return "Hostage rescued";
            if (Has("Kill_Hostage")) return "Hostage killed";
            if (Has("Damage_Hostage")) return "Hostage hurt";
            if (Has("Interact_Hostage")) return "Hostage";
            if (Has("Get_Killed")) return "Death";
            if (Has("Respawn")) return "Respawn";
            if (Has("Suicide")) return "Suicide";
            return "Award";
        }
        if (Has("Loser_Bonus")) return "Loss bonus";
        if (Has("Loser_Zero") || Has("no_income")) return "No income";
        if (Has("Planted_Bomb_But_Defused")) return "Plant bonus";
        if (Has("Hostage_Alive") || Has("Hostage_Interaction")) return "Hostage bonus";
        if (Has("Rescued_Hostage")) return "Hostage rescued";
        if (Has("Win") || Has("Elim")) return "Round win";
        return "Team award";
    }
}
