using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace AdminPlus;

/// <summary>
/// LEGACY-X match lifecycle:
/// - Match starts only at exactly 5 CT vs 5 T.
/// - Every active match player must explicitly ready.
/// - Match end triggers a soft map transition, not a process restart.
/// - The next map is selected randomly and cannot equal the previous map.
/// </summary>
public partial class AdminPlus
{
    private const int RequiredPlayersPerTeam = 5;
    private const float TransitionSeconds = 3.0f;

    private readonly HashSet<int> _readyUserIds = new();
    private readonly Random _random = new();
    private readonly string[] _matchMaps =
    {
        "de_ancient",
        "de_anubis",
        "de_inferno",
        "de_mirage",
        "de_nuke",
        "de_overpass",
        "de_vertigo",
        "de_dust2"
    };

    private string _lastSelectedMap = string.Empty;
    private bool _matchStarted;
    private bool _transitionInProgress;

    [GameEventHandler]
    public HookResult OnGameNewMap(EventGameNewmap @event, GameEventInfo info)
    {
        if (!string.IsNullOrWhiteSpace(@event.Mapname))
        {
            _lastSelectedMap = @event.Mapname;
            Console.WriteLine($"[LEGACY-X MatchFlow] Current map tracked: {_lastSelectedMap}");
        }

        _transitionInProgress = false;
        _matchStarted = false;
        _readyUserIds.Clear();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        // A changelevel causes the same plugin instance to receive the next round.
        // Clear transition state only after the new map has started its first round.
        if (_transitionInProgress)
        {
            _transitionInProgress = false;
            _matchStarted = false;
            _readyUserIds.Clear();
            Broadcast("New map loaded. Exactly 5v5 players must ready up.");
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnMatchFinished(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        if (!_matchStarted || _transitionInProgress)
        {
            return HookResult.Continue;
        }

        BeginSoftMapTransition();
        return HookResult.Continue;
    }

    [ConsoleCommand("css_ready", "Ready for the next LEGACY-X 5v5 match")]
    public void OnReady(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid || caller.IsBot)
        {
            return;
        }

        if (!caller.UserId.HasValue)
        {
            return;
        }

        _readyUserIds.Add(caller.UserId.Value);
        Broadcast($"{caller.PlayerName} is READY ({_readyUserIds.Count}/{ActiveHumanPlayers().Count}).");
        TryStartExactFiveVsFive();
    }

    [ConsoleCommand("css_unready", "Cancel LEGACY-X match ready state")]
    public void OnUnready(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid || caller.IsBot)
        {
            return;
        }

        if (caller.UserId.HasValue)
        {
            _readyUserIds.Remove(caller.UserId.Value);
        }
        Broadcast($"{caller.PlayerName} is no longer ready.");
    }

    [ConsoleCommand("css_match_status", "Show LEGACY-X exact 5v5 match status")]
    public void OnMatchStatus(CCSPlayerController? caller, CommandInfo info)
    {
        var players = ActiveHumanPlayers();
        var ct = players.Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist);
        var t = players.Count(p => p.TeamNum == (byte)CsTeam.Terrorist);
        var ready = players.Count(p => p.UserId.HasValue && _readyUserIds.Contains(p.UserId.Value));

        Reply(info, $"Match status: CT {ct}/5, T {t}/5, ready {ready}/{players.Count}, started={_matchStarted}");
    }

    private void TryStartExactFiveVsFive()
    {
        if (_matchStarted || _transitionInProgress)
        {
            return;
        }

        var players = ActiveHumanPlayers();
        var ct = players.Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist);
        var t = players.Count(p => p.TeamNum == (byte)CsTeam.Terrorist);

        // Important: 6v5, 5v6, 6v6 and every other imbalance are rejected.
        if (ct != RequiredPlayersPerTeam || t != RequiredPlayersPerTeam)
        {
            return;
        }

        // A ready button alone is not enough; all ten active match players must be ready.
        if (players.Count != RequiredPlayersPerTeam * 2 ||
            players.Any(player => !player.UserId.HasValue || !_readyUserIds.Contains(player.UserId.Value)))
        {
            return;
        }

        _matchStarted = true;
        _readyUserIds.Clear();
        Broadcast("Exact 5v5 confirmed. Match starting...");
        Server.ExecuteCommand("mp_warmup_end");
        AddTimer(0.5f, () => Server.ExecuteCommand("mp_restartgame 1"), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void BeginSoftMapTransition()
    {
        _transitionInProgress = true;
        _matchStarted = false;
        _readyUserIds.Clear();

        var nextMap = PickNextMap();
        _lastSelectedMap = nextMap;

        // changelevel produces the desired black/loading screen and reloads all map state
        // while keeping the CS2 server process alive. Previous result persistence must be
        // handled by the backend before this event in the production integration.
        Broadcast($"PLEASE WAIT — match finished. Loading a new map: {nextMap}");
        AddTimer(TransitionSeconds, () => Server.ExecuteCommand($"changelevel {nextMap}"), TimerFlags.STOP_ON_MAPCHANGE);
    }

    private string PickNextMap()
    {
        var candidates = _matchMaps
            .Where(map => !string.Equals(map, _lastSelectedMap, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return candidates.Length == 0
            ? _matchMaps[0]
            : candidates[_random.Next(candidates.Length)];
    }

    private List<CCSPlayerController> ActiveHumanPlayers()
    {
        return Utilities.GetPlayers()
            .Where(player => player.IsValid && !player.IsBot && player.TeamNum is (byte)CsTeam.CounterTerrorist or (byte)CsTeam.Terrorist)
            .ToList();
    }

    private void Broadcast(string message)
    {
        Console.WriteLine($"[LEGACY-X MatchFlow] {message}");
        Server.ExecuteCommand($"say [LEGACY-X] {message}");
    }
}
