using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegacyX.Admin.Authorization;
using Xunit;
using static LegacyX.Admin.Authorization.Tests.ParserTests;

namespace LegacyX.Admin.Authorization.Tests;

/// <summary>
/// A real HTTP server on 127.0.0.1 that behaves like the Root API route (token + plugin id check,
/// per-server roles, outages). Drives the client and store the way the plugin does: lookup on
/// connect, periodic refresh, failures, expiry.
/// </summary>
public sealed class LiveHttpTests : IAsyncLifetime
{
    private const string Token = "live-test-token-0123456789abcdef";
    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<(string Server, ulong SteamId), string> _roles = new();
    private volatile string _mode = "ok"; // ok | down | slow | garbage
    private string _baseUrl = "";
    private Task? _loop;
    public int Requests;

    public Task InitializeAsync()
    {
        var port = FreePort();
        _baseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(_baseUrl + "/");
        _listener.Start();
        _loop = Task.Run(ServeAsync);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _listener.Stop();
        if (_loop != null) await Task.WhenAny(_loop, Task.Delay(1000));
    }

    private static int FreePort()
    {
        var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); } catch { return; }
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        Interlocked.Increment(ref Requests);
        var response = context.Response;
        try
        {
            if (context.Request.Url!.AbsolutePath != "/api/v1/plugin/admin/authorizations" || context.Request.HttpMethod != "POST") { response.StatusCode = 404; return; }
            if (context.Request.Headers["authorization"] != $"Bearer {Token}") { response.StatusCode = 401; return; }
            if (context.Request.Headers["x-plugin-id"] != "legacyx-admin") { response.StatusCode = 403; return; }
            switch (_mode)
            {
                case "down": response.StatusCode = 503; return;
                case "slow": await Task.Delay(3000); break;
                case "garbage": await Write(response, "<html>oops</html>"); return;
            }
            var body = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var serverId = doc.RootElement.GetProperty("serverId").GetString()!;
            var entries = doc.RootElement.GetProperty("steamIds").EnumerateArray().Select(value =>
            {
                var steamId = ulong.Parse(value.GetString()!);
                return _roles.TryGetValue((serverId, steamId), out var role) ? Entry(steamId, true, role) : Entry(steamId, false, "player", "none");
            }).ToArray();
            await Write(response, Body(serverId, entries));
        }
        catch { response.StatusCode = 500; }
        finally { try { response.Close(); } catch { } }
    }

    private static async Task Write(HttpListenerResponse response, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.StatusCode = 200;
        await response.OutputStream.WriteAsync(bytes);
    }

    private AdminAuthorizationClient Client(string serverId = "srv-1", string token = Token, string pluginId = "legacyx-admin") =>
        new(new HttpClient(), _baseUrl, pluginId, token, serverId, TimeSpan.FromSeconds(1));

    /// <summary>What the plugin does on connect/refresh, returning the per-player change.</summary>
    private static async Task<Dictionary<ulong, AuthorizationChange>> Lookup(AdminAuthorizationClient client, AuthorizationStore store, DateTimeOffset now, params ulong[] ids)
    {
        var ticket = store.BeginRequest(ids);
        var result = await client.AuthorizeAsync(ids);
        return ids.ToDictionary(id => id, id => result.Success
            ? store.Apply(id, ticket, result.Players.TryGetValue(id, out var answer) && answer.IsStaff ? answer with { CheckedAt = now } : null, now)
            : store.ApplyFailure(id, ticket, now));
    }

    [Fact]
    public async Task Connect_refresh_revoke_and_role_change_against_a_live_http_api()
    {
        _roles[("srv-1", A)] = "owner";
        _roles[("srv-1", B)] = "staff";
        _roles[("srv-2", C)] = "admin"; // admin elsewhere only
        var client = Client();
        var store = new AuthorizationStore(TimeSpan.FromSeconds(180));

        var joined = await Lookup(client, store, Now, A, B, C);
        Assert.Equal(AuthorizationChange.Granted, joined[A]);
        Assert.Equal(AuthorizationChange.Granted, joined[B]);
        Assert.Equal(AuthorizationChange.Unchanged, joined[C]);
        Assert.Equal(StaffRole.Owner, store.Get(A, Now)!.Role);
        Assert.Null(store.Get(C, Now)); // server-specific: srv-2 role does not apply on srv-1

        _roles.TryRemove(("srv-1", B), out _);          // revoked on the website
        _roles[("srv-1", A)] = "manager";               // demoted
        var refreshed = await Lookup(client, store, Now.AddSeconds(60), A, B, C);
        Assert.Equal(AuthorizationChange.Updated, refreshed[A]);
        Assert.Equal(AuthorizationChange.Revoked, refreshed[B]);
        Assert.Equal(StaffRole.Manager, store.Get(A, Now.AddSeconds(60))!.Role);
        Assert.Null(store.Get(B, Now.AddSeconds(60)));
    }

    [Fact]
    public async Task Outage_grants_nothing_new_and_ages_out_existing_grants()
    {
        _roles[("srv-1", A)] = "admin";
        var client = Client();
        var store = new AuthorizationStore(TimeSpan.FromSeconds(180));
        await Lookup(client, store, Now, A);

        foreach (var mode in new[] { "down", "garbage", "slow" })
        {
            _mode = mode;
            _roles[("srv-1", B)] = "owner";
            var during = await Lookup(client, store, Now.AddSeconds(60), A, B);
            Assert.Equal(AuthorizationChange.Unchanged, during[A]); // still within cache age
            Assert.Null(store.Get(B, Now.AddSeconds(60)));          // nobody new while the API is unusable
        }

        var aged = await Lookup(client, store, Now.AddSeconds(181), A, B);
        Assert.Equal(AuthorizationChange.Revoked, aged[A]);
        Assert.Null(store.Get(A, Now.AddSeconds(181)));

        _mode = "ok";
        var recovered = await Lookup(client, store, Now.AddSeconds(200), A, B);
        Assert.Equal(AuthorizationChange.Granted, recovered[A]);
        Assert.Equal(AuthorizationChange.Granted, recovered[B]);
    }

    [Theory]
    [InlineData("wrong-token-0123456789abcdef-000", "legacyx-admin")]
    [InlineData(Token, "legacyx-community")]
    public async Task Wrong_credentials_grant_nothing(string token, string pluginId)
    {
        _roles[("srv-1", A)] = "owner";
        var store = new AuthorizationStore(TimeSpan.FromSeconds(180));
        var changes = await Lookup(Client(token: token, pluginId: pluginId), store, Now, A);
        Assert.Equal(AuthorizationChange.Unchanged, changes[A]);
        Assert.Null(store.Get(A, Now));
    }

    [Fact]
    public async Task Unreachable_api_grants_nothing()
    {
        var client = new AdminAuthorizationClient(new HttpClient(), $"http://127.0.0.1:{FreePort()}", "legacyx-admin", Token, "srv-1", TimeSpan.FromSeconds(1));
        var result = await client.AuthorizeAsync(new[] { A });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Parallel_lookups_from_one_client_are_safe()
    {
        _roles[("srv-1", A)] = "admin";
        var client = Client();
        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => client.AuthorizeAsync(new[] { A, B })));
        Assert.All(results, result =>
        {
            Assert.True(result.Success);
            Assert.Equal(StaffRole.Admin, result.Players[A].Role);
            Assert.Equal(StaffRole.Player, result.Players[B].Role);
        });
    }
}
