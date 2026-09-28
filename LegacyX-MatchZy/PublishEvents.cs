using System.Text;
using System.Text.Json;


namespace MatchZy
{
    public partial class MatchZy
    {
        public async Task SendEventAsync(MatchZyEvent @event)
        {
            try
            {
                // A match config's own remote log wins; otherwise the LEGACY-X rank endpoint from .env.
                var fromMatch = !string.IsNullOrEmpty(matchConfig.RemoteLogURL);
                var url = fromMatch ? matchConfig.RemoteLogURL : legacyXRankEventsUrl;
                var headerKey = fromMatch ? matchConfig.RemoteLogHeaderKey : "x-plugin-secret";
                var headerValue = fromMatch ? matchConfig.RemoteLogHeaderValue : legacyXRankToken;
                if (string.IsNullOrEmpty(url)) return;

                Log($"[SendEventAsync] Sending Event: {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} on {url}");

                using var httpClient = new HttpClient();
                using var jsonContent = new StringContent(JsonSerializer.Serialize(@event, @event.GetType()), Encoding.UTF8, "application/json");

                string jsonString = await jsonContent.ReadAsStringAsync();

                Log($"[SendEventAsync] SENDING DATA: {jsonString}");

                if (!string.IsNullOrEmpty(headerKey) && !string.IsNullOrEmpty(headerValue))
                {
                    httpClient.DefaultRequestHeaders.Add(headerKey, headerValue);
                }

                var httpResponseMessage = await httpClient.PostAsync(url, jsonContent);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} successful with status code: {httpResponseMessage.StatusCode}");
                }
                else
                {
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} failed with status code: {httpResponseMessage.StatusCode}, ResponseContent: {await httpResponseMessage.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception e)
            {
                Log($"[SendEventAsync FATAL] An error occurred: {e.Message}");
            }
        }
    }
}
