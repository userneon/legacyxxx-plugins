> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/SPECTATOR_COMMS_LEGACYX_CHANGELOG.md](https://github.com/userneon/legacyxxx-plugins/blob/main/SPECTATOR_COMMS_LEGACYX_CHANGELOG.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Spectator Comms Changelog

The Spectator Comms module implements the required anti-ghosting policy: spectator/dead players can text-chat only with their own spectator/dead channel; alive players can text-chat only with their own live team; both directions between spectator/dead and alive are blocked. It applies competitive voice cvars at startup and each round so a later MatchZy/server config execution cannot silently re-enable all-talk or dead-to-alive voice.

The plugin deliberately does not exempt public Match server coaches, staff or AFK-moved spectators. Any future official tournament coach exception requires a separate policy and audited module change.
