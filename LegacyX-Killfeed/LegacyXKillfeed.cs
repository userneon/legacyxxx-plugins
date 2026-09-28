using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;

namespace LegacyXKillfeed;

/// <summary>
/// Game → API: every kill on this server goes to POST /api/v1/plugin/killfeed/events (batched once a
/// second, up to 50 per request), where the website's live kill feed reads it. Kills made while the
/// API is unreachable are dropped rather than piling up: a kill feed is only worth showing live.
/// </summary>
public sealed class LegacyXKillfeed : BasePlugin
{
    private const int BatchSize = 50;
    private const int MaxQueued = 500;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly Regex WeaponChars = new("[^A-Za-z0-9_ -]", RegexOptions.Compiled);

    private readonly ConcurrentQueue<object> queue = new();
    private string apiUrl = "";
    private string token = "";
    private string serverId = "";
    private long counter;
    private int sending;

    public override string ModuleAuthor => "LEGACY-X";
    public override string ModuleName => "LEGACY-X Killfeed";
    public override string ModuleVersion => "0.1.0-legacyx.1";

    public override void Load(bool hotReload)
    {
        var environment = LegacyXEnvironmentLoader.Load();
        if (!environment.GetModuleBoolean("KILLFEED", "ENABLED", true))
        {
            Console.WriteLine($"[{ModuleName}] Disabled by central environment.");
            return;
        }
        var apiBase = environment.Get("LEGACYX_API_BASE_URL").Trim().TrimEnd('/');
        token = environment.GetModule("KILLFEED", "PLUGIN_TOKEN", environment.Get("LEGACYX_PLUGIN_TOKEN")).Trim();
        serverId = environment.Get("LEGACYX_SERVER_ID").Trim();
        if (apiBase.Length == 0 || token.Length < 20 || !Regex.IsMatch(serverId, "^[A-Za-z0-9._:-]{1,64}$"))
        {
            Console.WriteLine($"[{ModuleName}] Not sending: LEGACYX_API_BASE_URL, LEGACYX_PLUGIN_TOKEN or LEGACYX_SERVER_ID is missing in CounterStrikeSharp/.env.");
            return;
        }
        apiUrl = $"{apiBase}/api/v1/plugin/killfeed/events";
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        AddTimer(1.0f, Flush, TimerFlags.REPEAT);
        Console.WriteLine($"[{ModuleName}] Sending kills from {serverId}.");
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (victim == null || !victim.IsValid) return HookResult.Continue;
        var attacker = @event.Attacker;
        var hasAttacker = attacker != null && attacker.IsValid;
        if (queue.Count >= MaxQueued) queue.TryDequeue(out _);
        var weapon = WeaponChars.Replace(@event.Weapon ?? "", "");
        queue.Enqueue(new
        {
            event_id = $"kill:{serverId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Interlocked.Increment(ref counter)}",
            server_id = serverId,
            attacker_steam_id = hasAttacker && !attacker!.IsBot ? attacker.SteamID.ToString() : null,
            attacker_name = Name(hasAttacker ? attacker!.PlayerName : "World"),
            victim_steam_id = !victim.IsBot ? victim.SteamID.ToString() : null,
            victim_name = Name(victim.PlayerName),
            weapon = weapon.Length > 0 ? weapon[..Math.Min(weapon.Length, 64)] : "world",
            headshot = @event.Headshot,
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
        });
        return HookResult.Continue;
    }

    private void Flush()
    {
        if (queue.IsEmpty || Interlocked.Exchange(ref sending, 1) == 1) return;
        var batch = new List<object>(BatchSize);
        while (batch.Count < BatchSize && queue.TryDequeue(out var item)) batch.Add(item);
        _ = Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(batch), Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) Console.WriteLine($"[{ModuleName}] killfeed answered {(int)response.StatusCode}.");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{ModuleName}] killfeed unreachable: {exception.GetType().Name}.");
            }
            finally
            {
                Interlocked.Exchange(ref sending, 0);
            }
        });
    }

    private static string Name(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) text = "Player";
        return text.Length <= 64 ? text : text[..64];
    }
}
