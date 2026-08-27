using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using LegacyX.Shared.Configuration;

namespace LegacyX.Phantom;

public sealed class LegacyXPhantomConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public string CountMode { get; set; } = "per_player";
    public string MovementSource { get; set; } = "historical_round";
    public bool InteractionTelemetry { get; set; } = true;
    public int MovementDelayMs { get; set; } = 250;
    public int MaxCount { get; set; } = 64;
    public int TelemetryBatchSeconds { get; set; } = 5;
    public bool SuspensionEnabled { get; set; } = true;
    public int SuspensionScoreThreshold { get; set; } = 80;
    public int SuspensionMinimumSignals { get; set; } = 4;
    public double SuspensionMinimumConfidence { get; set; } = 0.8;
    public string ApiBaseUrl { get; set; } = "";
    public string PluginId { get; set; } = "legacyx-phantom";
    public string PluginSecret { get; set; } = "";
    public string ServerId { get; set; } = "legacyx-server";
    public string ServerMode { get; set; } = "community";
}

internal sealed class VirtualPhantom
{
    internal required Guid Id { get; init; }
    internal required ulong MappedSteamId { get; init; }
    internal required Vector Anchor { get; init; }
    internal required float Radius { get; init; }
    internal required float Phase { get; init; }
    internal Vector Position { get; set; } = new();
    internal int Samples { get; set; }
    internal DateTimeOffset LastEvidenceAt { get; set; }
}

internal sealed record PhantomEvidence(
    string event_id, string match_reference, string server_id, string server_mode, string steam_id, string phantom_id, string mapped_steam_id,
    object phantom_position, object player_position, int round_number, long tick, string interaction_type, int interaction_count,
    double aim_correlation, double movement_correlation, double wall_interaction, double shot_interaction, double suspicion_score, double evidence_confidence, string occurred_at);

internal sealed class PhantomSuspicion
{
    internal double Score { get; set; }
    internal int SignalCount { get; set; }
    internal double ConfidenceTotal { get; set; }
    internal HashSet<Guid> PhantomIds { get; } = [];
    internal double Confidence => SignalCount == 0 ? 0 : ConfidenceTotal / SignalCount;
}

internal sealed class PhantomSuspension
{
    internal required PhantomSuspicion Suspicion { get; init; }
    internal DateTimeOffset SuspendedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record PhantomSuspensionSignal(string event_id, string match_reference, string server_id, string server_mode, string steam_id, string event_type, int round_number, double suspicion_score, int evidence_count, object evidence_summary, string occurred_at);

public sealed class LegacyXPhantom : BasePlugin, IPluginConfig<LegacyXPhantomConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Dictionary<ulong, VirtualPhantom> _phantoms = new();
    private readonly Dictionary<ulong, PhantomSuspicion> _suspicion = new();
    private readonly Dictionary<ulong, PhantomSuspension> _suspended = new();
    private readonly Queue<PhantomEvidence> _pendingEvidence = new();
    private readonly object _evidenceLock = new();
    private DateTimeOffset _nextMovementAt = DateTimeOffset.MinValue;
    private int _roundNumber;
    private string _matchReference = string.Empty;
    public required LegacyXPhantomConfig Config { get; set; }

    public override string ModuleAuthor => "LEGACY-X Community";
    public override string ModuleName => "LEGACY-X Phantom";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public void OnConfigParsed(LegacyXPhantomConfig config)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        config.Enabled = environment.GetModuleBoolean("PHANTOM", "ENABLED", config.Enabled);
        config.CountMode = environment.GetModule("PHANTOM", "COUNT_MODE", config.CountMode);
        config.MovementSource = environment.GetModule("PHANTOM", "MOVEMENT_SOURCE", config.MovementSource);
        config.InteractionTelemetry = environment.GetModuleBoolean("PHANTOM", "INTERACTION_TELEMETRY", config.InteractionTelemetry);
        config.MovementDelayMs = environment.GetModuleInt("PHANTOM", "MOVEMENT_DELAY_MS", config.MovementDelayMs, 100, 5000);
        config.MaxCount = environment.GetModuleInt("PHANTOM", "MAX_COUNT", config.MaxCount, 1, 64);
        config.TelemetryBatchSeconds = environment.GetModuleInt("PHANTOM", "TELEMETRY_BATCH_SECONDS", config.TelemetryBatchSeconds, 1, 60);
        config.SuspensionEnabled = environment.GetModuleBoolean("PHANTOM", "SUSPENSION_ENABLED", config.SuspensionEnabled);
        config.SuspensionScoreThreshold = environment.GetModuleInt("PHANTOM", "SUSPENSION_SCORE_THRESHOLD", config.SuspensionScoreThreshold, 50, 100);
        config.SuspensionMinimumSignals = environment.GetModuleInt("PHANTOM", "SUSPENSION_MINIMUM_SIGNALS", config.SuspensionMinimumSignals, 2, 100);
        var confidenceRaw = environment.GetModule("PHANTOM", "SUSPENSION_MINIMUM_CONFIDENCE", config.SuspensionMinimumConfidence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        config.SuspensionMinimumConfidence = double.TryParse(confidenceRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedConfidence) ? Math.Clamp(parsedConfidence, 0.5, 1) : config.SuspensionMinimumConfidence;
        config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl).TrimEnd('/');
        config.PluginId = environment.GetModule("PHANTOM", "PLUGIN_ID", config.PluginId);
        config.PluginSecret = environment.GetModule("PHANTOM", "PLUGIN_TOKEN", config.PluginSecret);
        config.ServerId = environment.Get("LEGACYX_SERVER_ID", config.ServerId).Trim();
        config.ServerMode = environment.Get("LEGACYX_SERVER_MODE", config.ServerMode).Trim().ToLowerInvariant();
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        ResetMatchReference();
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterListener<Listeners.OnTick>(OnTick);
        AddTimer(Math.Max(1, Config.TelemetryBatchSeconds), () => _ = FlushEvidenceAsync(), TimerFlags.REPEAT);
        Console.WriteLine($"[{ModuleName}] Loaded — server-only virtual honeypots are {(Config.Enabled ? "enabled" : "disabled")}.");
    }

    public override void Unload(bool hotReload)
    {
        _phantoms.Clear();
        _suspicion.Clear();
        _suspended.Clear();
        lock (_evidenceLock) _pendingEvidence.Clear();
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (IsTrackable(@event.Userid))
        {
            EnsurePhantom(@event.Userid!.SteamID);
            _ = RestoreSuspensionAsync(@event.Userid);
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid != null)
        {
            var steamId = @event.Userid.SteamID;
            _phantoms.Remove(steamId);
            if (_suspended.TryGetValue(steamId, out var suspension)) _ = PublishSuspensionSignalAsync(steamId, suspension.Suspicion, "suspended_disconnect");
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundNumber++;
        if (_roundNumber == 1) ResetMatchReference();
        foreach (var player in Utilities.GetPlayers().Where(IsTrackable)) EnsurePhantom(player.SteamID);
        ReapplySuspensions();
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid != null && _suspended.ContainsKey(@event.Userid.SteamID)) ApplySuspension(@event.Userid);
        return HookResult.Continue;
    }

    private void OnTick()
    {
        if (!Config.Enabled || DateTimeOffset.UtcNow < _nextMovementAt) return;
        _nextMovementAt = DateTimeOffset.UtcNow.AddMilliseconds(Config.MovementDelayMs);
        var time = (float)Server.CurrentTime;
        foreach (var phantom in _phantoms.Values)
        {
            // Server-only fallback: independent procedural path. No real player position/input is read or copied.
            phantom.Position = new Vector(phantom.Anchor.X + MathF.Cos(time * 0.43f + phantom.Phase) * phantom.Radius, phantom.Anchor.Y + MathF.Sin(time * 0.37f + phantom.Phase) * phantom.Radius, phantom.Anchor.Z + MathF.Sin(time * 0.19f + phantom.Phase) * 8f);
        }
        ReapplySuspensions();
    }

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Config.Enabled || !IsTrackable(player)) return HookResult.Continue;
        if (_suspended.ContainsKey(player!.SteamID)) ApplySuspension(player);
        EvaluateShotCorrelation(player!);
        return HookResult.Continue;
    }

    private void EnsurePhantom(ulong mappedSteamId)
    {
        if (!Config.Enabled || _phantoms.ContainsKey(mappedSteamId) || _phantoms.Count >= Config.MaxCount || !Config.CountMode.Equals("per_player", StringComparison.OrdinalIgnoreCase)) return;
        var seed = HashCode.Combine(mappedSteamId, Server.MapName ?? "unknown", DateTimeOffset.UtcNow.DayOfYear);
        var random = new Random(seed);
        _phantoms[mappedSteamId] = new VirtualPhantom { Id = Guid.NewGuid(), MappedSteamId = mappedSteamId, Anchor = new Vector(random.Next(-2500, 2501), random.Next(-2500, 2501), random.Next(128, 513)), Radius = random.Next(96, 321), Phase = (float)(random.NextDouble() * Math.PI * 2) };
    }

    private void EvaluateShotCorrelation(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        var origin = pawn?.AbsOrigin;
        var angles = pawn?.EyeAngles;
        if (origin == null || angles == null) return;
        foreach (var phantom in _phantoms.Values)
        {
            if (phantom.MappedSteamId == player.SteamID || DateTimeOffset.UtcNow - phantom.LastEvidenceAt < TimeSpan.FromSeconds(3)) continue;
            var correlation = AimCorrelation(origin, angles, phantom.Position);
            if (correlation < 0.94) continue;
            phantom.LastEvidenceAt = DateTimeOffset.UtcNow;
            phantom.Samples++;
            var evidenceScore = Math.Min(100, 18 + phantom.Samples * 6);
            var evidenceConfidence = Math.Min(0.9, 0.45 + phantom.Samples * 0.05);
            EnqueueEvidence(player.SteamID, origin, phantom, "shot_correlation", correlation, evidenceScore, evidenceConfidence);
            TrackSuspicion(player, phantom, evidenceScore, evidenceConfidence);
        }
    }

    private void TrackSuspicion(CCSPlayerController player, VirtualPhantom phantom, double evidenceScore, double evidenceConfidence)
    {
        if (_suspended.ContainsKey(player.SteamID)) return;
        if (!_suspicion.TryGetValue(player.SteamID, out var suspicion)) _suspicion[player.SteamID] = suspicion = new PhantomSuspicion();
        suspicion.SignalCount++;
        suspicion.PhantomIds.Add(phantom.Id);
        suspicion.ConfidenceTotal += evidenceConfidence;
        suspicion.Score = Math.Min(100, suspicion.Score + evidenceScore * 0.35);
        if (!Config.SuspensionEnabled || suspicion.SignalCount < Config.SuspensionMinimumSignals || suspicion.Score < Config.SuspensionScoreThreshold || suspicion.Confidence < Config.SuspensionMinimumConfidence) return;
        _suspended[player.SteamID] = new PhantomSuspension { Suspicion = suspicion };
        ApplySuspension(player);
        _ = PublishSuspensionSignalAsync(player.SteamID, suspicion, "suspended");
        Server.NextFrame(() => PrintSuspensionNotice(player.SteamID));
    }

    private void ReapplySuspensions()
    {
        foreach (var steamId in _suspended.Keys.ToArray())
        {
            var player = Utilities.GetPlayerFromSteamId64(steamId);
            if (IsTrackable(player)) ApplySuspension(player!);
        }
    }

    private void ApplySuspension(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn == null || !pawn.IsValid) return;
        // Reapplied on tick, spawn, round start and reconnect. Real player position, aim,
        // health, score, team and all Phantom trajectories are intentionally untouched.
        try { pawn.MoveType = MoveType_t.MOVETYPE_NONE; pawn.ActualMoveType = MoveType_t.MOVETYPE_NONE; } catch { }
        try { pawn.TakesDamage = false; pawn.VelocityModifier = 0f; } catch { }
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
    }

    private void PrintSuspensionNotice(ulong steamId)
    {
        var player = Utilities.GetPlayerFromSteamId64(steamId);
        if (!IsTrackable(player)) return;
        player!.PrintToChat("{red}LEGACY-X ANTI-CHEAT: {white}YOU HAVE BEEN SUSPENDED.");
        player.PrintToChat("{yellow}YOU ARE TEMPORARILY RESTRICTED FROM GAMEPLAY WHILE YOUR CASE IS REVIEWED.");
        player.PrintToChat("{white}PLEASE DO NOT DISCONNECT FROM THE SERVER.");
        player.PrintToChat("{red}DISCONNECTING DURING AN ACTIVE SUSPENSION MAY RESULT IN A PERMANENT BAN AFTER REVIEW.");
        player.PrintToChat("{green}PLEASE WAIT FOR A MANAGER TO REVIEW YOUR CASE.");
        player.PrintToChat("{cyan}legacyx.cc/appeal");
    }

    private static double AimCorrelation(Vector origin, QAngle angles, Vector target)
    {
        var dx = target.X - origin.X; var dy = target.Y - origin.Y; var dz = target.Z - origin.Z;
        var horizontal = MathF.Sqrt(dx * dx + dy * dy);
        if (horizontal < 256 || horizontal > 6000) return 0;
        var expectedYaw = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        var expectedPitch = -MathF.Atan2(dz, horizontal) * 180f / MathF.PI;
        var yawError = MathF.Abs(NormalizeAngle(angles.Y - expectedYaw));
        var pitchError = MathF.Abs(NormalizeAngle(angles.X - expectedPitch));
        return Math.Max(0, 1 - Math.Max(yawError / 8f, pitchError / 8f));
    }

    private void EnqueueEvidence(ulong steamId, Vector playerPosition, VirtualPhantom phantom, string type, double aim, double score, double confidence)
    {
        if (!Config.InteractionTelemetry) return;
        var occurredAt = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var eventId = $"phantom-{Config.ServerId}-{_matchReference.GetHashCode():x}-{nonce}";
        var evidence = new PhantomEvidence(eventId, _matchReference, Config.ServerId, Config.ServerMode, steamId.ToString(), phantom.Id.ToString(), phantom.MappedSteamId.ToString(), new { x = phantom.Position.X, y = phantom.Position.Y, z = phantom.Position.Z }, new { x = playerPosition.X, y = playerPosition.Y, z = playerPosition.Z }, _roundNumber, Environment.TickCount64, type, phantom.Samples, aim, 0, 0, aim, score, confidence, occurredAt.ToString("O"));
        lock (_evidenceLock) _pendingEvidence.Enqueue(evidence);
    }

    private async Task FlushEvidenceAsync()
    {
        if (!Config.Enabled || string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret)) return;
        var batch = new List<PhantomEvidence>();
        lock (_evidenceLock) while (_pendingEvidence.Count > 0 && batch.Count < 50) batch.Add(_pendingEvidence.Dequeue());
        foreach (var evidence in batch)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiBaseUrl}/api/v1/plugin/phantom/evidence");
                request.Headers.Add("x-plugin-id", Config.PluginId);
                request.Headers.Add("x-plugin-secret", Config.PluginSecret);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(JsonSerializer.Serialize(evidence), Encoding.UTF8, "application/json");
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"API status {(int)response.StatusCode}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{ModuleName}] Evidence delivery failed: {exception.Message}");
                lock (_evidenceLock) _pendingEvidence.Enqueue(evidence);
            }
        }
    }

    private async Task RestoreSuspensionAsync(CCSPlayerController player)
    {
        if (!Config.SuspensionEnabled || string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret)) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Config.ApiBaseUrl}/api/v1/plugin/phantom/suspensions/{player.SteamID}?serverId={Uri.EscapeDataString(Config.ServerId)}");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("suspension", out var remote) || remote.ValueKind != JsonValueKind.Object) return;
            var score = remote.TryGetProperty("suspicion_score", out var scoreElement) && scoreElement.TryGetDouble(out var remoteScore) ? remoteScore : Config.SuspensionScoreThreshold;
            var evidenceCount = remote.TryGetProperty("evidence_count", out var countElement) && countElement.TryGetInt32(out var remoteCount) ? remoteCount : Config.SuspensionMinimumSignals;
            var suspicion = new PhantomSuspicion { Score = score, SignalCount = evidenceCount, ConfidenceTotal = evidenceCount * Config.SuspensionMinimumConfidence };
            _suspended[player.SteamID] = new PhantomSuspension { Suspicion = suspicion };
            ApplySuspension(player);
            _ = PublishSuspensionSignalAsync(player.SteamID, suspicion, "restored");
            Server.NextFrame(() => PrintSuspensionNotice(player.SteamID));
        }
        catch (Exception exception) { Console.WriteLine($"[{ModuleName}] Suspension restore lookup failed: {exception.Message}"); }
    }

    private async Task PublishSuspensionSignalAsync(ulong steamId, PhantomSuspicion suspicion, string eventType)
    {
        if (string.IsNullOrWhiteSpace(Config.ApiBaseUrl) || string.IsNullOrWhiteSpace(Config.PluginSecret)) return;
        try
        {
            var occurredAt = DateTimeOffset.UtcNow;
            var payload = new PhantomSuspensionSignal($"phantom-suspension-{Config.ServerId}-{Guid.NewGuid():N}", _matchReference, Config.ServerId, Config.ServerMode, steamId.ToString(), eventType, _roundNumber, Math.Round(suspicion.Score, 2), suspicion.SignalCount, new { phantom_ids = suspicion.PhantomIds.Select(id => id.ToString()).ToArray(), latest_interaction = "shot_correlation", evidence_confidence = Math.Round(suspicion.Confidence, 3) }, occurredAt.ToString("O"));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiBaseUrl}/api/v1/plugin/phantom/suspensions");
            request.Headers.Add("x-plugin-id", Config.PluginId);
            request.Headers.Add("x-plugin-secret", Config.PluginSecret);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) Console.WriteLine($"[{ModuleName}] Suspension signal rejected: {(int)response.StatusCode}");
        }
        catch (Exception exception) { Console.WriteLine($"[{ModuleName}] Suspension signal delivery failed: {exception.Message}"); }
    }

    private void ResetMatchReference()
    {
        _roundNumber = 0;
        _matchReference = $"{Config.ServerId}:{Server.MapName ?? "unknown"}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static float NormalizeAngle(float angle) { while (angle > 180) angle -= 360; while (angle < -180) angle += 360; return angle; }
    private static bool IsTrackable(CCSPlayerController? player) => player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.SteamID > 0;
}
