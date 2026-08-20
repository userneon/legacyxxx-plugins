# LEGACY-X MatchFlow

## Match start rule

LEGACY-X MatchFlow нь match-ийг зөвхөн **яг 5 CT vs 5 T** болсон үед эхлүүлнэ. `6v5`, `5v6`, `6v6`, `4v5` болон бусад бүх тохиолдол match start gate-ээс татгалзана. Бүх арван active human player `css_ready` командаар ready болсон байх ёстой.

```text
css_ready       Ready for the next match
css_unready     Cancel the caller's ready state
css_match_status Show CT/T/ready state
```

To make the in-game ENTER key submit ready without overwriting the player's default binding globally, the LEGACY-X match server may publish this optional bind instruction in its rules/Discord setup:

```text
bind ENTER css_ready
```

The server should show `ENTER = READY` in the lobby instructions. The plugin does not silently overwrite existing client keybinds.

Ready болсон ч player count яг 5v5 биш бол match эхлэхгүй. Spectator болон bot-ууд active match player count-д орохгүй. Match эхлэх үед warmup дуусч, `mp_restartgame 1`-ээр шинэ match state үүснэ.

## Match end behavior

Match дуусахад hard process restart хийхгүй. Plugin дараах soft transition хийнэ.

```text
Match end event
→ match result/stat persistence hook
→ [LEGACY-X] PLEASE WAIT notification
→ three-second transition window
→ changelevel <random-next-map>
→ new map loaded
→ old ready/match state cleared
→ exact 5v5 ready gate дахин эхэлнэ
```

CS2-ийн `changelevel` нь server process-ийг дахин асаахгүйгээр map state, round state, player state-ийг шинээр үүсгэнэ. Тоглогчдын дэлгэц дээр map loading/black transition гарч, шинэ map автоматаар ачаална.

## Map rotation

Map pool:

- `de_ancient`
- `de_anubis`
- `de_inferno`
- `de_mirage`
- `de_nuke`
- `de_overpass`
- `de_vertigo`
- `de_dust2`

`game_newmap` event-ээр одоогийн map-ийг tracking хийж, дараагийн random сонголтоос өмнөх map-ийг хасна. Ингэснээр өмнөх map дахин шууд давтагдахгүй. Production server дээр map pool-ийг official installed maps-тэй адил байлгах хэрэгтэй.

## Production notes

Match result, demo болон web audit persistence нь map change-ээс өмнө backend/plugin integration-ээр хадгалагдах ёстой. `changelevel` нь player reconnect хийх hard restart биш; connection continuity-г аль болох хадгалах soft transition юм. Хэрэв server process crash эсвэл maintenance хийвэл л hard restart хэрэглэнэ.

Actual CS2 server дээр AdminPlus DLL-г `addons/counterstrikesharp/plugins/AdminPlus/` дотор байрлуулж, CounterStrikeSharp reload/restart хийсний дараа `css_match_status` командаар gate-ийг шалгана.
