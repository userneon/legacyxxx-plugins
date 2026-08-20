# LEGACY-X Reconnect Changelog

`LegacyXReconnect` is a custom CounterStrikeSharp module for Last Played and safe reconnect behavior. It tracks player sessions, reports a server heartbeat, and lets a player run `css_reconnect` from a LEGACY-X server to join their most recent eligible, online session on a different registered server.

The plugin does not auto-connect players after a true process crash, because client-side auto-reconnect cannot be guaranteed by a server plugin. It preserves the reliable path: CS2 map transitions remain owned by MatchZy, while a player who returns to a LEGACY-X server can explicitly use the validated reconnect command.
