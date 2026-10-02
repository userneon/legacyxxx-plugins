> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-MatchZy/documentation/docs/getting_started.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-MatchZy/documentation/docs/getting_started.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

By default, the plugin will start in `pug` mode, which means teams won't be locked. Players and admins can use [available commands](../commands)
Below are the available modes of the plugin:

1. **Pug mode** (by default)
2. **Practice mode** - This can be started by admins using `.prac` command
3. **Scrim mode** - This can be toggled by admins using `.playout` command. It will enable playout of all rounds.
4. **Match mode** - This can be set-up by providing a [match json](../match_setup)

Below are some basic admin commands which can be used for the initial configuration:

- `.start` Force starts a match. 
- `.restart` Force restarts/resets a match.
- `.readyrequired <number>` Sets the number of ready players required to start the match. If set to 0, all connected players will have to ready-up to start the match.
- `.roundknife` Toggles the knife round. If disabled, match will directly go from Warmup phase to Live phase.
- `.map <mapname>` Changes the map
