# LEGACY-X Admin — In-game Command Reference

Энэ жагсаалт нь `LegacyX-Admin` source-д бүртгэгдсэн command-ууд дээр үндэслэсэн. Chat дотор `!` prefix ашиглана. Console alias нь `css_` prefix-тэй; жишээ нь `!ban` болон `css_ban` нь ижил handler руу орно.

> Энэ бол command inventory. OWNER, MANAGER, ADMIN зэрэг website/staff role-д яг аль command нээхийг тусдаа allowlist policy-оор шийднэ. Frontend visibility нь permission биш; plugin тал өөрөө permission шалгана.

## Target хэлбэр

| Хэлбэр | Тайлбар |
|---|---|
| `<target>` | Player name, `#userid`, SteamID эсвэл тухайн command зөвшөөрсөн selector |
| `all` / `@all` | Бүх player |
| `@t`, `@ct`, `@spec` | Terrorist, Counter-Terrorist, Spectator group |
| `[value]` | Optional argument |

## Player moderation

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!ban` | `css_ban` | `!ban <nick/steamid/ip/userid> [duration] [reason]` | Player ban |
| `!ipban` | `css_ipban` | `!ipban <nick/ip/steamid/userid> [reason]` | IP ban |
| `!unban` | `css_unban` | `!unban <nick/ip/steamid>` | Ban цуцлах |
| `!lastban` | `css_lastban` | `!lastban` | Сүүлд disconnect хийсэн player list |
| `!baninfo` | `css_baninfo` | `!baninfo <target>` | Ban мэдээлэл |
| `!kick` | `css_kick` | `!kick <nick> [reason]` | Server-ээс гаргах |
| `!mute` | `css_mute` | `!mute <target> [duration] [reason]` | Voice mute |
| `!gag` | `css_gag` | `!gag <target> [duration] [reason]` | Text chat gag |
| `!unmute` | `css_unmute` | `!unmute <target>` | Voice mute цуцлах |
| `!ungag` | `css_ungag` | `!ungag <target>` | Gag цуцлах |
| `!silence` | `css_silence` | `!silence <target> [duration] [reason]` | Mute + gag |
| `!unsilence` | `css_unsilence` | `!unsilence <target>` | Mute + gag цуцлах |
| `!mutelist` | `css_mutelist` | `!mutelist` | Mute list |
| `!gaglist` | `css_gaglist` | `!gaglist` | Gag list |

## Player and server actions

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!slap` | `css_slap` | `!slap <target> <damage>` | Damage slap |
| `!slay` | `css_slay` | `!slay <target>` | Player kill |
| `!money` | `css_money` | `!money <target> <amount>` | Money set |
| `!armor` | `css_armor` | `!armor <target> <amount>` | Armor set |
| `!rr` | `css_rr` | `!rr` | Round restart |
| `!map` | `css_map` | `!map <map>` | Map change |
| `!wsmap` / `!workshop` | `css_wsmap` / `css_workshop` | `!wsmap <workshop_id>` | Workshop map change |
| `!who` | `css_who` | `!who <nick/steamid>` | Player identity/info |
| `!players` | `css_players` | `!players` | Console player list |
| `!rename` | `css_rename` | `!rename <target> <name>` | Player rename |
| `!team` | `css_team` | `!team <target> <t|ct|spec>` | Team placement |
| `!swap` | `css_swap` | `!swap <target>` | Team swap |

## Announcement and communication

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!asay` | `css_asay` | `!asay <message>` | Admin chat announcement |
| `!csay` | `css_csay` | `!csay <message>` | Center-screen announcement |
| `!hsay` | `css_hsay` | `!hsay <message>` | HUD announcement |
| `!psay` | `css_psay` | `!psay <target> <message>` | Player private message |
| `!admins` | `css_admins` | `!admins` | Online admins list |
| `!hideadmin` | `css_hideadmin` | `!hideadmin` | `!admins` list-д харагдах эсэхийг солих |
| `css_report` / `css_calladmin` | — | Report arguments server config-аас хамаарна | Player `!admin` / report → optional Call channel alert |

## Vote and match utility

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!vote` | `css_vote` | `!vote <question> <option1> <option2> [option3...]` | Custom vote |
| `!votemap` | `css_votemap` | `!votemap <map1> <map2> [map3...]` | Map vote |
| `!rvote` | `css_rvote` | `!rvote` | Vote дахин нээх |
| `!cancelvote` | `css_cancelvote` | `!cancelvote` | Active vote цуцлах |
| `!votekick` | `css_votekick` | `!votekick` | Kick vote |
| `!voteban` | `css_voteban` | `!voteban` | Ban vote |
| `!votegag` | `css_votegag` | `!votegag` | Gag vote |
| `!votemute` | `css_votemute` | `!votemute` | Mute vote |
| `!votesilence` | `css_votesilence` | `!votesilence` | Silence vote |

## Advanced and fun commands

Эдгээр нь source дээр `@css/slay`, `@css/cheats`, `@css/kick` permission-оор бүртгэгдсэн. Competitive/Pro League дээр default-оор нээхгүй байх нь зүйтэй.

| Permission | Commands |
|---|---|
| `@css/slay` | `css_freeze`, `css_unfreeze`, `css_gravity`, `css_bury`, `css_unbury`, `css_beacon`, `css_shake`, `css_unshake`, `css_blind`, `css_unblind`, `css_clean`, `css_goto`, `css_bring`, `css_hrespawn`, `css_1up`, `css_drug`, `css_undrug`, `css_glow`, `css_color` |
| `@css/cheats` | `css_revive`, `css_respawn`, `css_noclip`, `css_weapon`, `css_strip`, `css_sethp`, `css_hp`, `css_speed`, `css_unspeed`, `css_god` |
| `@css/kick` | `css_team`, `css_swap` |

## High-risk owner-only commands

| Command | Эрсдэл | Proposed policy |
|---|---|---|
| `!rcon <command>` / `css_rcon` | Arbitrary server command | OWNER-only; website browser UI-д raw RCON огт бүү гарга |
| `!cvar <cvar> [value]` / `css_cvar` | Runtime server setting өөрчилнө | OWNER-only; allowlist-тэй executor ашиглах |
| `css_cleanbans`, `css_cleanipbans`, `css_cleansteambans` | Historical penalty data mass-delete | Default disabled; manual database backup + explicit owner procedure шаардлагатай |
| `css_cleanall`, `css_cleanmute`, `css_cleangag` | Punishment cleanup | Default disabled; audit-required owner operation |
| `!addadmin`, `!removeadmin`, `!adminreload` | In-game admin source өөрчилнө | OWNER-only; canonical `staff` database model-тэй reconcile хийхээс өмнө production-д нээхгүй |

## Plugin administration

| Command | Console alias | Үйлдэл |
|---|---|---|
| `!admin` | `css_admin`, `css_adminmenu` | Admin menu нээх |
| `!banlist` | `css_banlist` | Ban list menu |
| `!adminhelp` | `css_adminhelp` | Detailed command help |
| `!adminlist` | `css_adminlist` | Plugin admin list |
| `!addadmin` | `css_addadmin` | Plugin admin add |
| `!removeadmin` | `css_removeadmin` | Plugin admin remove |
| `!adminreload` | `css_adminreload`, `css_admin_reload` | Plugin admin cache reload |
| `!version` | `css_version` | LEGACY-X Admin version |

## Permission model next step

Source-ийн generic permission namespaces нь `@css/root`, `@css/ban`, `@css/kick`, `@css/slay`, `@css/cheats`, `@css/generic`, `@css/reservation` байна. Дараагийн алхамд OWNER, MANAGER, ADMIN болон бусад community role тус бүрт эдгээрээс яг ямар permission/command өгөхийг explicit allowlist болгон шийднэ.
