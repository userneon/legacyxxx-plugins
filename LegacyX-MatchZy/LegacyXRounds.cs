using System.Text;
using System.Text.Json;

namespace MatchZy;

/// <summary>
/// The round timeline of a match's details (owner request 2026-10-30, "rounds are not saved"): every finished round of a Match Core match is posted
/// to the site with its winning side, the reason and the score. Fire and forget; a refused round is only logged and never touches the match.
/// </summary>
public partial class MatchZy
{
    private void PostLegacyXRound(int winnerTeam, int reason, int team1Score, int team2Score, int roundNumber, int mapNumber)
    {
        try
        {
            if (!IsMatchCoreEnabled || !matchCoreId.HasValue || matchCoreCreateFailed || roundNumber < 1) return;
            // Built here, on the game thread; the request is made on a worker thread.
            var body = JsonSerializer.Serialize(new
            {
                match_id = matchCoreId.Value.ToString(),
                map_number = mapNumber,
                round_number = roundNumber,
                winner_side = winnerTeam == 2 ? "t" : winnerTeam == 3 ? "ct" : null,
                reason,
                team1_score = team1Score,
                team2_score = team2Score,
            });
            var url = legacyXMatchCoreApiUrl.Value.Trim();
            var slash = url.LastIndexOf('/');
            if (slash < 0) return;
            url = url[..(slash + 1)] + "round";
            var secret = legacyXMatchCorePluginSecret.Value.Trim();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Add("x-plugin-id", "legacyx-match-core");
                    request.Headers.Add("x-plugin-secret", secret);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    using var response = await matchCoreHttp.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var text = await response.Content.ReadAsStringAsync();
                        Log($"[LegacyXMatchCore] Round {roundNumber} was refused ({(int)response.StatusCode}): {text[..Math.Min(200, text.Length)]}");
                    }
                }
                catch (Exception exception)
                {
                    Log($"[LegacyXMatchCore] Round {roundNumber} could not be sent: {exception.Message}");
                }
            });
        }
        catch (Exception exception)
        {
            Log($"[LegacyXMatchCore] Round {roundNumber} could not be built: {exception.Message}");
        }
    }
}
