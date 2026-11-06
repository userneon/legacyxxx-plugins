using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using System;
using System.Linq;

namespace AdminPlus;

/// <summary>
/// Troll commands that draw a joke card on the target's HUD (owner request 2026-11-06). The card is drawn by LegacyX-Hud (`lx_troll_hud`, layout legacyx_troll.xml in
/// the Workshop addon); without that plugin or the addon nothing is shown. Every card but the scare turns into "JUST KIDDING" before it goes. All @css/slay.
/// Nothing is announced to the server (it would spoil the joke); the admin gets a line and the action is logged. UNTESTED in game.
///   css_fakeban / css_fakevac / css_fakecrash / css_fakehack &lt;t&gt; [seconds]     css_scare &lt;t&gt; [text]     css_banner &lt;t&gt; &lt;text&gt;
/// </summary>
public partial class AdminPlus
{
    private void RegisterFunTrollCommands()
    {
        AddPermCmd("@css/slay", "css_fakeban", "<#userid|name|all @ commands> [seconds]", (c, i) => CmdTroll(c, i, "ban", "fakeban"));
        AddPermCmd("@css/slay", "css_fakevac", "<#userid|name|all @ commands> [seconds]", (c, i) => CmdTroll(c, i, "vac", "fakevac"));
        AddPermCmd("@css/slay", "css_fakecrash", "<#userid|name|all @ commands> [seconds]", (c, i) => CmdTroll(c, i, "crash", "fakecrash"));
        AddPermCmd("@css/slay", "css_fakehack", "<#userid|name|all @ commands> [seconds]", (c, i) => CmdTroll(c, i, "hack", "fakehack"));
        AddPermCmd("@css/slay", "css_scare", "<#userid|name|all @ commands> [text]", (c, i) => CmdTroll(c, i, "scare", "scare"));
        AddPermCmd("@css/slay", "css_banner", "<#userid|name|all @ commands> <text>", (c, i) => CmdTroll(c, i, "banner", "banner"));
    }

    private void CmdTroll(CCSPlayerController? caller, CommandInfo info, string kind, string name)
    {
        var usage = kind == "banner" ? "<target> <text>" : kind == "scare" ? "<target> [text]" : "<target> [seconds]";
        if (!ResolveFunTargets(caller, info, $"Usage: css_{name} {usage}", $"Fun.{char.ToUpperInvariant(name[0])}{name[1..]}.Usage", out var players, out var admin, out var label)) return;

        var text = "";
        var seconds = kind switch { "scare" => 1.5f, "banner" => 6f, _ => 8f };
        if (kind is "scare" or "banner")
        {
            // Everything after the target is the text; the HUD plugin accepts letters, digits, spaces and . , ! ? ' + - : ( ) only.
            var words = Enumerable.Range(2, Math.Max(0, info.ArgCount - 2)).Select(info.GetArg);
            text = new string(string.Join(' ', words).Where(ch => char.IsLetterOrDigit(ch) || " .,!?'+-:()".Contains(ch)).ToArray()).Trim();
            if (text.Length > 70) text = text[..70];
            if (kind == "banner" && text.Length == 0) { LogError("css_banner needs a text"); return; }
        }
        else seconds = SecondsArg(info, 2, seconds, 3f, 15f) ?? seconds;

        foreach (var t in players)
        {
            if (!t.IsValid || t.IsBot) continue;
            var line = $"lx_troll_hud {t.Slot} {kind} {seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}";
            if (text.Length > 0) line += $" \"{text}\"";
            CounterStrikeSharp.API.Server.ExecuteCommand(line);
        }
        caller?.PrintToChat($" \x04[LEGACY-X]\x01 Sent {kind} to {label}.");
        AdminPlus.LogAction($"{admin} played the {kind} joke on {label}");
    }
}
