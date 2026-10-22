# LegacyX-Emotes

Sells the animated Fortnite-style emotes and dances (owner decision 2026-10-22). [FortniteEmotesNDances](https://github.com/Cruze03/FortniteEmotesNDances)
plays them; this plugin only decides **who may**: an emote plays when the player owns it in the Shop (paid with LX), otherwise they get
"You don't own this emote. Get it in the Shop on the website." Owners and root admins play any emote when `LEGACYX_EMOTES_ROOT_PLAYS_ALL=true`.
It is closed by default: with the API unreachable or unconfigured only root can play.

The upstream project states no license, and the emotes and dances are Epic Games' work: selling them is the owner's own risk and decision.

## Install on a game server
1. Install FortniteEmotesNDances and its dependencies (Metamod, CounterStrikeSharp v361+, MultiAddonManager, Ray-Trace) as its README says,
   and add Workshop addon `3328582199` to `mm_extra_addons`.
2. Deploy this plugin next to it (`LegacyX-Emotes.dll`). It reuses the Community plugin's API credentials (`LEGACYX_COMMUNITY_PLUGIN_ID` / `_TOKEN`),
   or set `LEGACYX_EMOTES_PLUGIN_ID` / `LEGACYX_EMOTES_PLUGIN_TOKEN`. `LEGACYX_API_BASE_URL` is shared.
3. In the server console run `lx_emotes_sql`. It writes `legacyx-emotes-shop.sql` (next to the plugin) with a Shop row for every emote and dance
   the server has (300 LX per emote, 500 per dance as a start). Edit the prices, then run `legacyxxx-backend/supabase/legacy_x_cosmetics_emotes.sql`
   and that file in Supabase. Emotes then appear in Shop → Emotes.

The emote names in the Shop are the names in FortniteEmotesNDances' config (`game_key`); renaming one there detaches it from its Shop row.
`third-party/FortniteEmotesAPI` holds only the plugin's public interface, so this project compiles; it is never deployed.
