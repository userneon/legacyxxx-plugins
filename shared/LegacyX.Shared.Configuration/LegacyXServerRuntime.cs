namespace LegacyX.Shared.Configuration;

// Host-level runtime settings. Plugins do not open RCON connections by default;
// a future reviewed executor may opt in to this contract without reading browser/database secrets.
public sealed record LegacyXServerRuntime(
    string ServerId,
    string ServerAddress,
    string ServerMode,
    string RconHost,
    int RconPort,
    string RconPassword)
{
    public bool HasRconConfiguration =>
        !string.IsNullOrWhiteSpace(RconHost) &&
        RconPort is >= 1 and <= 65535 &&
        RconPassword.Length >= 16;

    public static LegacyXServerRuntime Load(LegacyXEnvironment environment) => new(
        environment.Get("LEGACYX_SERVER_ID").Trim(),
        environment.Get("LEGACYX_SERVER_ADDRESS").Trim(),
        environment.Get("LEGACYX_SERVER_MODE", "competitive_5v5").Trim(),
        environment.Get("LEGACYX_RCON_HOST").Trim(),
        environment.GetInt("LEGACYX_RCON_PORT", 27015, 1, 65535),
        environment.Get("LEGACYX_RCON_PASSWORD"));
}
