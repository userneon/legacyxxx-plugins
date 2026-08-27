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

public sealed class LegacyXPhantom : BasePlugin, IPluginConfig<LegacyXPhantomConfig>
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly Dictionary<ulong, VirtualPhantom> _phantoms = new();
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
        RegisterEventHandler<EventWeaponFire>(OnWeaponFire);
        RegisterListener<Listeners.OnTick>(OnTick);
        AddTimer(Math.Max(1, Config.TelemetryBatchSeconds), () => _ = FlushEvidenceAsync(), TimerFlags.REPEAT);
        Console.WriteLine($"[{ModuleName}] Loaded — server-only virtual honeypots are {(Config.Enabled ? "enabled" : "disabled")}.");
    }

    public override void Unload(bool hotReload)
    {
        _phantoms.Clear();
        lock (_evidenceLock) _pendingEvidence.Clear();
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (IsTrackable(@event.Userid)) EnsurePhantom(@event.Userid!.SteamID);
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid != null) _phantoms.Remove(@event.Userid.SteamID);
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundNumber++;
        if (_roundNumber == 1) ResetMatchReference();
        foreach (var player in Utilities.GetPlayers().Where(IsTrackable)) EnsurePhantom(player.SteamID);
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
    }

    private HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (!Config.Enabled || !IsTrackable(player)) return HookResult.Continue;
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
            EnqueueEvidence(player.SteamID, origin, phantom, "shot_correlation", correlation, Math.Min(100, 18 + phantom.Samples * 6), Math.Min(0.9, 0.45 + phantom.Samples * 0.05));
        }
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

    private void ResetMatchReference()
    {
        _roundNumber = 0;
        _matchReference = $"{Config.ServerId}:{Server.MapName ?? "unknown"}:{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static float NormalizeAngle(float angle) { while (angle > 180) angle -= 360; while (angle < -180) angle += 360; return angle; }
    private static bool IsTrackable(CCSPlayerController? player) => player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.SteamID > 0;
}
