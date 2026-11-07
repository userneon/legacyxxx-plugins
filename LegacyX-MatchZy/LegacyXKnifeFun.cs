using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace MatchZy;

/// <summary>
/// The round before a match that decides the sides is a random little game instead of always the knife (owner request 2026-11-07, "make the knife
/// round not only the knife but a fun game, random"). Each time one of these is picked: how the players are armed and the gravity. It is still one
/// round, won by the team that stands last, so the side vote and everything after it work as before. matchzy_knife_fun_rounds 0 brings the plain knife round back.
/// </summary>
public partial class MatchZy
{
    public FakeConVar<bool> knifeFunRounds = new("matchzy_knife_fun_rounds", "Whether the round that decides the sides is a random fun game (knife, AWP only, Deagle only...) instead of always the knife. Default: true", true);

    /// <param name="Name">Shown to the players in chat.</param>
    /// <param name="Primary">Weapon given to both teams (empty: none).</param>
    /// <param name="Secondary">Pistol given to both teams (empty: none).</param>
    /// <param name="Gravity">sv_gravity for the round (800 is the normal one).</param>
    private sealed record KnifeFunGame(string Name, string Primary, string Secondary, int Gravity);

    private static readonly KnifeFunGame[] KnifeFunGames =
    {
        new("Knife fight", "", "", 800),
        new("AWP only", "weapon_awp", "", 800),
        new("Deagle only", "", "weapon_deagle", 800),
        new("Scout on the moon", "weapon_ssg08", "", 300),
        new("Knives on the moon", "", "", 300),
        new("Pistol duel", "", "weapon_p250", 800),
    };

    private const int NormalGravity = 800;
    private bool knifeFunActive;

    /// <summary>Picks a game and sets it up. Called right after the knife round's config, before the game restarts into the round.</summary>
    private void StartKnifeFun()
    {
        knifeFunActive = false;
        if (!knifeFunRounds.Value) return;
        var game = KnifeFunGames[Random.Shared.Next(KnifeFunGames.Length)];
        knifeFunActive = true;
        Server.ExecuteCommand($"mp_t_default_primary \"{game.Primary}\";mp_ct_default_primary \"{game.Primary}\";mp_t_default_secondary \"{game.Secondary}\";mp_ct_default_secondary \"{game.Secondary}\";sv_gravity {game.Gravity};");
        Log($"[KnifeFun] Opening round game: {game.Name}");
        // The chat line of the knife round comes with the restart; this one names the game.
        AddTimer(1.5f, () => { if (isKnifeRound) PrintToAllChat($"{ChatColors.White}Opening round: {game.Name}.{ChatColors.Grey} The team left standing picks the side."); });
    }

    /// <summary>Puts the weapons and the gravity back (before the side vote, or when a match is cancelled in the middle of the round).</summary>
    private void EndKnifeFun()
    {
        if (!knifeFunActive) return;
        knifeFunActive = false;
        Server.ExecuteCommand($"mp_t_default_primary \"\";mp_ct_default_primary \"\";mp_t_default_secondary \"\";mp_ct_default_secondary \"\";sv_gravity {NormalGravity};");
    }
}
