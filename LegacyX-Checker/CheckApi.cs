using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LegacyX.Checker;

public sealed class CheckApiException : Exception
{
    public CheckApiException(string message) : base(message) { }
}

/// <summary>The three things the program asks the website: who wants this check, and here is the result.</summary>
public sealed class CheckApi
{
    // Empty fields are left out: the server refuses a field that is present but null.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private readonly HttpClient _http;

    public CheckApi(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"LegacyX-Checker/{App.Version}");
    }

    /// <summary>A server that answered with an error is not the same as one that cannot be reached; say which, with the status number.</summary>
    private static string Describe(HttpRequestException problem, string noConnection) =>
        problem.StatusCode is { } status ? $"The server answered with an error ({(int)status}). Try again in a minute, and tell staff if it keeps happening." : noConnection;

    private static string Path(string code) => $"api/v1/checks/code/{Uri.EscapeDataString(code)}";

    public async Task<CodeInfo> GetCodeAsync(string code, CancellationToken cancel = default)
    {
        try
        {
            using var response = await _http.GetAsync(Path(code), cancel);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new CheckApiException("That code is not valid or has expired. Ask for a new one.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new CheckApiException("Too many tries. Wait a minute and try again.");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CodeInfo>(Json, cancel) ?? throw new CheckApiException("The server sent an answer this program does not understand.");
        }
        catch (HttpRequestException problem)
        {
            throw new CheckApiException(Describe(problem, "Could not reach the LEGACY-X server. Check your internet connection."));
        }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new CheckApiException("The server took too long to answer. Try again.");
        }
    }

    /// <summary>The lists to look for, from the server. Null when it cannot be reached or does not answer: the plain copy inside the program is used then.</summary>
    public async Task<Rules?> GetRulesAsync(string code, CancellationToken cancel = default)
    {
        try
        {
            using var response = await _http.GetAsync(Path(code) + "/rules", cancel);
            if (!response.IsSuccessStatusCode) return null;
            return Rules.FromServer(await response.Content.ReadAsStringAsync(cancel));
        }
        catch
        {
            return null;
        }
    }

    public async Task SendReportAsync(string code, CheckReport report, CancellationToken cancel = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(Path(code) + "/report", report, Json, cancel);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new CheckApiException("The code ended before the scan finished. Ask for a new one.");
            if (response.StatusCode == HttpStatusCode.Conflict) throw new CheckApiException("This code was already used.");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException problem)
        {
            throw new CheckApiException(Describe(problem, "Could not send the result. Check your internet connection.") + " Your report is saved on your Desktop.");
        }
    }
}
