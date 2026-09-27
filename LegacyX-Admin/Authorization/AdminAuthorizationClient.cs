using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LegacyX.Admin.Authorization;

/// <summary>Outcome of one lookup. On failure <see cref="Players"/> is empty and <see cref="Error"/> says why (never the token).</summary>
public sealed record AuthorizationResult(bool Success, IReadOnlyDictionary<ulong, PlayerAuthorization> Players, string? Error)
{
    public static AuthorizationResult Failed(string error) => new(false, new Dictionary<ulong, PlayerAuthorization>(), error);
}

/// <summary>
/// Calls POST {apiBaseUrl}/api/v1/plugin/admin/authorizations with the admin plugin's scoped token.
/// Never throws: timeouts, network errors, non-2xx answers and malformed bodies all come back as a
/// failed <see cref="AuthorizationResult"/>, which callers treat as "not authorized".
/// </summary>
public sealed class AdminAuthorizationClient
{
    public const int MaxPlayersPerRequest = 64;

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly string _pluginId;
    private readonly string _pluginToken;
    private readonly string _serverId;
    private readonly TimeSpan _timeout;
    private readonly Func<DateTimeOffset> _clock;

    public AdminAuthorizationClient(HttpClient http, string apiBaseUrl, string pluginId, string pluginToken, string serverId, TimeSpan timeout, Func<DateTimeOffset>? clock = null)
    {
        _http = http;
        _endpoint = new Uri(apiBaseUrl.TrimEnd('/') + "/api/v1/plugin/admin/authorizations");
        _pluginId = pluginId;
        _pluginToken = pluginToken;
        _serverId = serverId;
        _timeout = timeout;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string ServerId => _serverId;

    public async Task<AuthorizationResult> AuthorizeAsync(IReadOnlyCollection<ulong> steamIds, CancellationToken cancellationToken = default)
    {
        var ids = steamIds.Distinct().ToList();
        var players = new Dictionary<ulong, PlayerAuthorization>();
        foreach (var chunk in ids.Chunk(MaxPlayersPerRequest))
        {
            var part = await AuthorizeChunkAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (!part.Success) return part;
            foreach (var pair in part.Players) players[pair.Key] = pair.Value;
        }
        return new AuthorizationResult(true, players, null);
    }

    private async Task<AuthorizationResult> AuthorizeChunkAsync(IReadOnlyCollection<ulong> steamIds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            var body = JsonSerializer.Serialize(new { serverId = _serverId, steamIds = steamIds.Select(id => id.ToString()).ToArray() });
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _pluginToken);
            request.Headers.Add("x-plugin-id", _pluginId);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return AuthorizationResult.Failed($"API answered HTTP {(int)response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var parsed = AuthorizationResponseParser.Parse(json, _serverId, steamIds, _clock(), out var error);
            return parsed == null
                ? AuthorizationResult.Failed($"invalid API response: {error}")
                : new AuthorizationResult(true, parsed, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthorizationResult.Failed($"API did not answer within {_timeout.TotalSeconds:0.#}s");
        }
        catch (OperationCanceledException)
        {
            return AuthorizationResult.Failed("request cancelled");
        }
        catch (HttpRequestException ex)
        {
            return AuthorizationResult.Failed($"API unreachable: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AuthorizationResult.Failed($"API request failed: {ex.GetType().Name}");
        }
    }
}
