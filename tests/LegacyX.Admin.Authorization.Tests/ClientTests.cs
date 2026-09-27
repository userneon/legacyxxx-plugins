using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LegacyX.Admin.Authorization;
using Xunit;
using static LegacyX.Admin.Authorization.Tests.ParserTests;

namespace LegacyX.Admin.Authorization.Tests;

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> _respond;
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = new();

    public FakeHandler(Func<HttpRequestMessage, string, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) Requests.Add((request, body));
        return await _respond(request, body, cancellationToken);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Answers like the backend: every requested SteamID, with the roles given.</summary>
    public static string Answer(string requestBody, IReadOnlyDictionary<ulong, string> roles)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var serverId = doc.RootElement.GetProperty("serverId").GetString()!;
        var entries = doc.RootElement.GetProperty("steamIds").EnumerateArray().Select(id =>
        {
            var steamId = ulong.Parse(id.GetString()!);
            return roles.TryGetValue(steamId, out var role) ? Entry(steamId, true, role) : Entry(steamId, false, "player", "none");
        });
        return Body(serverId, entries.ToArray());
    }
}

public class ClientTests
{
    private const string Token = "secret-plugin-token-0123456789abcdef";

    private static AdminAuthorizationClient Client(FakeHandler handler, double timeoutSeconds = 2) =>
        new(new HttpClient(handler), "https://api.example.test/", "legacyx-admin", Token, "srv-1", TimeSpan.FromSeconds(timeoutSeconds), () => Now);

    [Fact]
    public async Task Sends_the_documented_request()
    {
        var handler = new FakeHandler((_, body, _) => Task.FromResult(FakeHandler.Json(HttpStatusCode.OK, FakeHandler.Answer(body, new Dictionary<ulong, string> { [A] = "admin" }))));
        var result = await Client(handler).AuthorizeAsync(new[] { A, B, A });

        Assert.True(result.Success);
        Assert.Equal(StaffRole.Admin, result.Players[A].Role);
        Assert.Equal(StaffRole.Player, result.Players[B].Role);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.test/api/v1/plugin/admin/authorizations", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Token, request.Headers.Authorization.Parameter);
        Assert.Equal("legacyx-admin", request.Headers.GetValues("x-plugin-id").Single());
        Assert.Equal($"{{\"serverId\":\"srv-1\",\"steamIds\":[\"{A}\",\"{B}\"]}}", body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Fails_closed_on_http_errors(HttpStatusCode status)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(FakeHandler.Json(status, $"{{\"error\":\"echo {Token}\"}}")));
        var result = await Client(handler).AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
        Assert.Empty(result.Players);
        Assert.Contains(((int)status).ToString(), result.Error);
        Assert.DoesNotContain(Token, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>bad gateway</html>")]
    [InlineData("{\"serverId\":\"another-server\",\"players\":[]}")]
    public async Task Fails_closed_on_invalid_bodies(string body)
    {
        var handler = new FakeHandler((_, _, _) => Task.FromResult(FakeHandler.Json(HttpStatusCode.OK, body)));
        var result = await Client(handler).AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
        Assert.StartsWith("invalid API response", result.Error);
    }

    [Fact]
    public async Task Fails_closed_on_timeout_without_waiting_forever()
    {
        var handler = new FakeHandler(async (_, _, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return FakeHandler.Json(HttpStatusCode.OK, "{}");
        });
        var started = DateTime.UtcNow;
        var result = await Client(handler, timeoutSeconds: 0.3).AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
        Assert.Contains("did not answer", result.Error);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Fails_closed_when_the_api_is_unreachable()
    {
        var handler = new FakeHandler((_, _, _) => throw new HttpRequestException("Connection refused"));
        var result = await Client(handler).AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
        Assert.Contains("unreachable", result.Error);
        Assert.DoesNotContain(Token, result.Error);
    }

    [Fact]
    public async Task Never_throws_on_unexpected_handler_errors()
    {
        var handler = new FakeHandler((_, _, _) => throw new InvalidOperationException(Token));
        var result = await Client(handler).AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
        Assert.DoesNotContain(Token, result.Error);
    }

    [Fact]
    public async Task Splits_large_servers_into_requests_of_64_and_fails_if_any_part_fails()
    {
        var ids = Enumerable.Range(0, 100).Select(i => 76561198000001000UL + (ulong)i).ToArray();
        var handler = new FakeHandler((_, body, _) => Task.FromResult(FakeHandler.Json(HttpStatusCode.OK, FakeHandler.Answer(body, new Dictionary<ulong, string> { [ids[99]] = "manager" }))));
        var result = await Client(handler).AuthorizeAsync(ids);
        Assert.True(result.Success);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(100, result.Players.Count);
        Assert.Equal(StaffRole.Manager, result.Players[ids[99]].Role);

        var calls = 0;
        var failing = new FakeHandler((_, body, _) => Task.FromResult(Interlocked.Increment(ref calls) == 2
            ? FakeHandler.Json(HttpStatusCode.BadGateway, "")
            : FakeHandler.Json(HttpStatusCode.OK, FakeHandler.Answer(body, new Dictionary<ulong, string> { [ids[0]] = "owner" }))));
        var partial = await Client(failing).AuthorizeAsync(ids);
        Assert.False(partial.Success);
        Assert.Empty(partial.Players);
    }
}
