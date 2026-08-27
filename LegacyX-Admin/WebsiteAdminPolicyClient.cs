using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AdminPlus;

internal sealed class WebsiteAdminPolicyClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _apiBaseUrl;
    private readonly string _pluginId;
    private readonly string _pluginSecret;

    internal WebsiteAdminPolicyClient(string apiBaseUrl, string pluginId, string pluginSecret)
    {
        _apiBaseUrl = apiBaseUrl;
        _pluginId = pluginId;
        _pluginSecret = pluginSecret;
    }

    internal async Task<WebsiteAdminPolicyResponse> GetPolicyAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _apiBaseUrl + "/api/v1/plugin/admin-policy");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _pluginSecret);
        request.Headers.Add("x-plugin-id", _pluginId);
        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"LEGACY-X API returned {(int)response.StatusCode}: {detail[..Math.Min(200, detail.Length)]}");
        }
        return await response.Content.ReadFromJsonAsync<WebsiteAdminPolicyResponse>() ?? new WebsiteAdminPolicyResponse();
    }
}

internal sealed class WebsiteAdminPolicyResponse
{
    [JsonPropertyName("policyVersion")] public string PolicyVersion { get; set; } = string.Empty;
    [JsonPropertyName("admins")] public List<WebsiteAdminPolicyEntry> Admins { get; set; } = [];
}

internal sealed class WebsiteAdminPolicyEntry
{
    [JsonPropertyName("steamId")] public string SteamId { get; set; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; set; } = "LEGACY-X Staff";
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("stamina")] public int Stamina { get; set; }
    [JsonPropertyName("immunity")] public int Immunity { get; set; }
    [JsonPropertyName("permissions")] public List<string> Permissions { get; set; } = [];
    internal bool IsUsable => System.Text.RegularExpressions.Regex.IsMatch(SteamId, "^7656\\d{13,14}$") && Permissions.Count > 0 && Stamina is >= 0 and <= 1000 && Immunity is >= 0 and <= 1000;
}
