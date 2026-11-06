# Deploy checklist, 2026-11-06 (бүх шинэчлэл)

Дараалал: **Backend → Frontend → Discord bot → Plugins (VPS) → Workshop (Windows) → CS2AC → Шалгалт.**
Бүх алхмын өмнө `git pull` заавал хийнэ (compile хийхээсээ өмнө).

## 1. Backend (VPS)
```bash
cd ~/legacyxxx-backend && git pull
npm ci && npm run build
pm2 restart legacy-x-api && pm2 logs legacy-x-api --lines 20 --nostream
```
Шалгах: `curl -s https://api.legacyx.cc/api/v1/public/competitive/leaderboard | head -c 200` (ladder ирэх ёстой).
Нэг удаа (хийгдсэн, хэрэв дахин шаардвал): `supabase/legacy_x_leaders_hide_banned.sql`, `supabase/legacy_x_anticheat_flags.sql`.

## 2. Frontend (VPS)
```bash
cd ~/legacyxxx-frontend && bash ops/deploy.sh          # legacyx.cc
bash ops/deploy-admin.sh                                # admin.legacyx.cc
```

## 3. Discord bot (VPS)
```bash
cd ~/legacyxxx-discord-bot && git pull && npm ci
pm2 restart all     # эсвэл: pm2 restart <bot-ийн нэр>
pm2 logs --lines 30 --nostream | grep -iE "rank-roles|banned"
```
Bot-д **Manage Channels** эрх хэрэгтэй (Banned role-ийн channel нуух). Log-д `[rank-roles] banned role: N channel(s) updated` гарна.

## 4. Plugins (VPS)
```bash
cd ~/legacyxxx-plugins && git pull
git log --oneline -3                      # хамгийн сүүлийн commit харагдах ёстой
dotnet --list-sdks                        # 8.0.x ба 10.0.x (Hud-д) хоёулаа
node scripts/package-plugins.mjs 2>&1 | tail -15
scripts/deploy.sh /home/cs2/cs2/game/csgo
sudo systemctl restart cs2@27015
```
Шалгах (dll шинэ эсэх):
```bash
P=/home/cs2/cs2/game/csgo/addons/counterstrikesharp/plugins
ls -l --time-style=full-iso $P/LegacyX-Hud/LegacyX-Hud.dll $P/LegacyX-Admin/LegacyX-Admin.dll
grep -c -a 'lx_welcome_hud' $P/LegacyX-Hud/LegacyX-Hud.dll        # >= 1
grep -c -a 'lx_troll_hud'   $P/LegacyX-Hud/LegacyX-Hud.dll        # >= 1
grep -c -a 'css_cs2ac_flag' $P/LegacyX-Admin/LegacyX-Admin.dll    # >= 1
```
Хуучин файл цэвэрлэх: `rm -rf $P/LegacyX-AntiWallhack /home/cs2/cs2/game/csgo/addons/counterstrikesharp/configs/retake` ; `.env`-ээс `ANTIWALLHACK` мөр устгах.

## 5. Workshop addon (Windows)
```
cd legacyxxx-workshop
git pull
tools\build.cmd -Addon legacyx2
```
Build log-д `legacyx_welcome`, `legacyx_troll` compile хийгдсэн байх ёстой. Дараа нь Workshop Manager-аар **item 3830785234**-ийг дахин publish хийнэ.
Өөрийн PC-д: `tools\build.cmd -Unlocal`, CS2-г бүрэн хааж дахин нээнэ. Хэрэгтэй бол client кэш:
`Remove-Item -Recurse -Force "C:\Program Files (x86)\Steam\steamapps\workshop\content\730\3830785234"`
VPS-д addon шинэ эсэх:
```bash
D=/home/cs2/cs2/game/bin/linuxsteamrt64/steamapps/workshop/content/730/3830785234
ls -l --time-style=full-iso $D
for n in legacyx_welcome legacyx_troll legacyx_menu; do echo -n "$n: "; grep -c -a "$n" $D/3830785234_dir.vpk; done
```

## 6. CS2AC (нэг удаа)
```bash
cd ~/legacyxxx-plugins
sudo ./scripts/cs2-host.sh cs2ac install
sudo ./scripts/cs2-host.sh cs2ac whitelist <root-ийн steamid64,steamid64>
sudo ./scripts/cs2-host.sh cs2ac mode log        # эхлээд log; дараа нь: mode ban
sudo ./scripts/cs2-host.sh cs2ac status
sudo systemctl restart cs2@27015
```
Серверийн консол: `meta list`, `cs2ac_status`, `cs2ac_check_config`.

## 7. Тоглоомон дээр шалгах
| Шалгах зүйл | Яаж | Хүлээж буй үр дүн |
|---|---|---|
| Welcome card | Team сонгох (шинэ restart-ийн дараа) | Голд карт 6 секунд |
| Skin menu Refresh | `!ws` эсвэл E барих | Refresh икон товч |
| Ban screen | консол: `lx_troll_hud 0 banned 5 "Permanent. Test"` | Бүтэн дэлгэц улаан 5 сек |
| Troll карт | `!fakeban <нэр>` | "YOU HAVE BEEN BANNED" → "JUST KIDDING" |
| Жинхэнэ ban | `!ban <нэр> 1 test` | Царцаа + улаан дэлгэц + улаан chat + салгагдана |
| Wallhack toy | `!wh <нэр>` | Дайсан хананы цаанаас гэрэлтэнэ |
| Bhop | `!bhop <нэр>` | Jump барихад хурд хадгалж hop |
| Aimlock | `!aimlock <нэр>` + буудах | Нэг дайсан руу зөөлөн эргэнэ |
| Fun командууд | `!jetpack`, `!chicken`, `!pop`, `!aura` | Тус бүр ажиллах/эсвэл log-д алдаа |

## 8. Шинэ ban-ы урсгал (профайл, ladder, Discord)
1. Туршилтын тоглогчид `!ban <нэр> 5 test` өг.
2. Сайт: `legacyx.cc/profile/<steamid>` → **Banned** гарчиг, карт бүр "Banned".
3. `legacyx.cc/leaders` → тэр тоглогч алга.
4. Discord (холбосон бол, ≤5 мин): `Banned` role, зөвхөн support channel харагдана.
5. `css_unban <steamid64>` → тоо, жагсаалт, role бүгд буцна (≤5 мин).

## 9. Анхаарах
- `git pull` мартвал хуучин dll/addon үлдэнэ (өнөөдрийн ихэнх асуудал).
- CS2AC-ийг эхлээд `mode log`-д ажиллуулж detection хараад `mode ban` руу шилжүүл.
- Aimlock, bhop, fun, troll, ban screen, CS2AC интеграци: тоглоомон дээр **анх удаа** туршигдаж байна. Алдаа гарвал `journalctl -u cs2@27015 --since "10 min ago" | grep -iE "AdminPlus|LEGACY-X"`-ийн гаралтыг явуул.
