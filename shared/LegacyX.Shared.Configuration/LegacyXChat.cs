namespace LegacyX.Shared.Configuration;

/// <summary>
/// Canonical presentation for system-generated LEGACY-X chat messages.
/// Player-authored chat must not be sent through this formatter.
/// </summary>
public static class LegacyXChat
{
    public const string Prefix = "{green}LEGACY-X • {default}";

    public static string System(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Prefix.TrimEnd();
        return $"{Prefix}{message.Trim().ToUpperInvariant()}";
    }

    public static string MatchConnect(string address) => System($"IP: CONNECT {address}");
}
