using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Commands;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Usage: dotnet run -- <path to dist/legacyx-cs2>
var packageRoot = Path.GetFullPath(args.Length > 0 ? args[0] : "dist/legacyx-cs2");
var cssRoot = Path.Combine(packageRoot, "addons", "counterstrikesharp");
var failures = new List<string>();
void Pass(string message) => Console.WriteLine($"PASS  {message}");
void Fail(string message) { failures.Add(message); Console.WriteLine($"FAIL  {message}"); }

// --- shared/ exactly like PluginManager.LoadSharedLibraries + the Default.Resolving hook ---
var shared = new Dictionary<string, Assembly>();
foreach (var directory in Directory.GetDirectories(Path.Combine(cssRoot, "shared")))
{
    var path = Path.Combine(directory, Path.GetFileName(directory) + ".dll");
    if (!File.Exists(path)) continue;
    var assembly = PluginLoader.CreateFromAssemblyFile(path, new[] { typeof(IPlugin), typeof(PluginCapability<>), typeof(PlayerCapability<>) }, config => config.PreferSharedTypes = true).LoadDefaultAssembly();
    shared[assembly.GetName().Name!] = assembly;
    Pass($"shared library loaded: {Path.GetRelativePath(cssRoot, path)}");
}
AssemblyLoadContext.Default.Resolving += (_, name) => shared.TryGetValue(name.Name!, out var assembly) ? assembly : null;

var external = new HashSet<string>();

var loadedPlugins = new Dictionary<string, (Assembly Assembly, AssemblyLoadContext Context)>();
foreach (var directory in Directory.GetDirectories(Path.Combine(cssRoot, "plugins")).OrderBy(d => d))
{
    var name = Path.GetFileName(directory);
    var path = Path.Combine(directory, name + ".dll");
    try
    {
        // Same shared types and config as PluginContext.Load.
        var loader = PluginLoader.CreateFromAssemblyFile(path, new[] { typeof(IPlugin), typeof(ILogger), typeof(IServiceCollection), typeof(IPluginServiceCollection<>), typeof(ICommandManager) }, config =>
        {
            config.EnableHotReload = false;
            config.IsUnloadable = true;
            config.PreferSharedTypes = true;
        });
        var assembly = loader.LoadDefaultAssembly();
        var context = AssemblyLoadContext.GetLoadContext(assembly)!;
        loadedPlugins[name] = (assembly, context);

        var unresolved = new List<string>();
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (external.Contains(reference.Name!)) continue;
            try
            {
                var dependency = context.LoadFromAssemblyName(reference);
                var from = shared.TryGetValue(reference.Name!, out var sharedAssembly) && ReferenceEquals(dependency, sharedAssembly) ? "shared/"
                    : AssemblyLoadContext.GetLoadContext(dependency) == context ? "plugin folder"
                    : "CounterStrikeSharp / .NET runtime";
                if (reference.Name == "LegacyX.Shared.Configuration" && !ReferenceEquals(dependency, shared.GetValueOrDefault(reference.Name!)))
                    unresolved.Add("LegacyX.Shared.Configuration did not come from shared/");
                Console.WriteLine($"      {name} → {reference.Name} {dependency.GetName().Version} ({from})");
            }
            catch (Exception ex) { unresolved.Add($"{reference.Name}: {ex.GetType().Name}"); }
        }

        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            var missing = ex.LoaderExceptions.Where(e => e != null).Select(e => e!.Message).Distinct().ToList();
            // Types that need MenuManagerApi are expected to fail here without the MenuManager plugin.
            if (missing.All(message => external.Any(message.Contains))) { types = ex.Types.Where(t => t != null).ToArray()!; Console.WriteLine($"      {name}: some types need {string.Join(", ", external)} (provided by the MenuManager plugin on the server)"); }
            else throw new InvalidOperationException(string.Join("; ", missing));
        }

        var pluginTypes = types.Where(t => t is { IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(t)).ToList();
        if (unresolved.Count > 0) Fail($"{name}: unresolved references: {string.Join(", ", unresolved)}");
        else if (pluginTypes.Count != 1) Fail($"{name}: expected one IPlugin type, found {pluginTypes.Count}");
        else Pass($"{name}: loaded, {types.Length} types, plugin class {pluginTypes[0].FullName}");
    }
    catch (Exception ex)
    {
        Fail($"{name}: {ex.GetType().Name}: {ex.Message}");
    }
}

// --- MatchZy: the packaged native SQLite library loads through the plugin's load context ---
if (loadedPlugins.TryGetValue("LegacyX-MatchZy", out var matchZy))
{
    try
    {
        var batteries = matchZy.Context.LoadFromAssemblyName(new AssemblyName("SQLitePCLRaw.batteries_v2")).GetType("SQLitePCL.Batteries_V2", throwOnError: true)!;
        batteries.GetMethod("Init")!.Invoke(null, null);
        var raw = matchZy.Context.LoadFromAssemblyName(new AssemblyName("SQLitePCLRaw.core")).GetType("SQLitePCL.raw", throwOnError: true)!;
        var version = raw.GetMethod("sqlite3_libversion_number")!.Invoke(null, null);
        Pass($"LegacyX-MatchZy: native SQLite from the package works (sqlite {version})");
    }
    catch (Exception ex) { Fail($"LegacyX-MatchZy native SQLite: {(ex.InnerException ?? ex).Message}"); }
}

// --- LegacyX-Admin: the packaged authorization client against a local API, fail closed ---
if (loadedPlugins.TryGetValue("LegacyX-Admin", out var admin))
{
    const string token = "package-load-test-token-0123456789";
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var http = new HttpListener();
    http.Prefixes.Add($"http://127.0.0.1:{port}/");
    http.Start();
    var mode = "ok";
    _ = Task.Run(async () =>
    {
        while (http.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await http.GetContextAsync(); } catch { return; }
            var ok = ctx.Request.Headers["authorization"] == $"Bearer {token}" && ctx.Request.Headers["x-plugin-id"] == "legacyx-admin";
            if (!ok || mode == "down") { ctx.Response.StatusCode = ok ? 503 : 401; ctx.Response.Close(); continue; }
            var body = "{\"serverId\":\"srv-load\",\"checkedAt\":\"2026-09-27T12:00:00Z\",\"players\":[" +
                       "{\"steamId\":\"76561198000000001\",\"authorized\":true,\"role\":\"owner\",\"status\":\"active\",\"source\":\"global\",\"expiresAt\":null}," +
                       "{\"steamId\":\"76561198000000002\",\"authorized\":false,\"role\":\"player\",\"status\":\"revoked\",\"source\":\"server\",\"expiresAt\":null}]}";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    });

    try
    {
        var clientType = admin.Assembly.GetType("LegacyX.Admin.Authorization.AdminAuthorizationClient", throwOnError: true)!;
        async Task<(bool Success, IDictionary<ulong, object> Players, string? Error)> Authorize(string useToken)
        {
            var client = Activator.CreateInstance(clientType, new HttpClient(), $"http://127.0.0.1:{port}", "legacyx-admin", useToken, "srv-load", TimeSpan.FromSeconds(2), null)!;
            var task = (Task)clientType.GetMethod("AuthorizeAsync")!.Invoke(client, new object[] { new ulong[] { 76561198000000001, 76561198000000002 }, CancellationToken.None })!;
            await task;
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var players = ((System.Collections.IEnumerable)result.GetType().GetProperty("Players")!.GetValue(result)!).Cast<object>()
                .ToDictionary(pair => (ulong)pair.GetType().GetProperty("Key")!.GetValue(pair)!, pair => pair.GetType().GetProperty("Value")!.GetValue(pair)!);
            return ((bool)result.GetType().GetProperty("Success")!.GetValue(result)!, players, (string?)result.GetType().GetProperty("Error")!.GetValue(result));
        }
        string Role(object authorization) => authorization.GetType().GetProperty("Role")!.GetValue(authorization)!.ToString()!;

        var ok = await Authorize(token);
        if (ok.Success && Role(ok.Players[76561198000000001]) == "Owner" && Role(ok.Players[76561198000000002]) == "Player") Pass("LegacyX-Admin (packaged): owner authorized, revoked player denied over HTTP");
        else Fail($"LegacyX-Admin (packaged): unexpected result {ok.Success} {ok.Error}");

        var badToken = await Authorize("wrong-token-000000000000000000000");
        if (!badToken.Success && badToken.Players.Count == 0 && !(badToken.Error ?? "").Contains("wrong-token")) Pass($"LegacyX-Admin (packaged): wrong token → no permissions ({badToken.Error})");
        else Fail("LegacyX-Admin (packaged): wrong token was not refused");

        mode = "down";
        var down = await Authorize(token);
        if (!down.Success && down.Players.Count == 0) Pass($"LegacyX-Admin (packaged): API outage → no permissions ({down.Error})");
        else Fail("LegacyX-Admin (packaged): outage did not fail closed");
    }
    catch (Exception ex) { Fail($"LegacyX-Admin authorization client: {(ex.InnerException ?? ex).Message}"); }
    finally { http.Stop(); }
}

// --- Shared configuration: one instance serves every plugin and reads the central .env ---
if (shared.TryGetValue("LegacyX.Shared.Configuration", out var configAssembly))
{
    var envFile = Path.Combine(Path.GetTempPath(), $"legacyx-load-{Guid.NewGuid():N}.env");
    File.WriteAllText(envFile, "LEGACYX_SERVER_ID=srv-load\nLEGACYX_ADMIN_AUTH_REFRESH_SECONDS=90\n");
    Environment.SetEnvironmentVariable("LEGACYX_ENV_FILE", envFile);
    var loaderType = configAssembly.GetType("LegacyX.Shared.Configuration.LegacyXEnvironmentLoader", throwOnError: true)!;
    var environment = loaderType.GetMethod("Load")!.Invoke(null, null)!;
    var refresh = environment.GetType().GetMethod("GetModuleInt")!.Invoke(environment, new object[] { "ADMIN", "AUTH_REFRESH_SECONDS", 60, 15, 600 });
    File.Delete(envFile);
    if ((int)refresh! == 90) Pass("shared LegacyX.Shared.Configuration reads LEGACYX_ADMIN_AUTH_REFRESH_SECONDS from the central .env");
    else Fail($"shared configuration returned {refresh}");
}

Console.WriteLine(failures.Count == 0 ? $"\nALL PACKAGE LOAD CHECKS PASSED ({loadedPlugins.Count} plugins)" : $"\n{failures.Count} FAILURE(S)");
return failures.Count == 0 ? 0 : 1;
