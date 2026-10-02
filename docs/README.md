# legacyxxx-plugins: баримтын бүртгэл

CS2 server дээр ажилладаг plugin-ууд ба тэднийг суулгах, ажиллуулах скриптүүд. Баримт бүр доор ангилагдсан.
Таван repo бүр өөрийн `docs/README.md` (бүртгэл) ба `docs/MANUAL_MN.md` (монгол гарын авлага)-той.

## Эндээс эхэл

| Хэрэгтэй зүйл | Унших |
|---|---|
| Game server (VPS) суулгах, update, нөөцлөлт, команд бүр | [MANUAL_MN.md](MANUAL_MN.md) |
| Plugin бүрийн тайлбар | [readmes/1-game-server-plugins](readmes/1-game-server-plugins/00-repo.md) |
| Admin эрх ба in-game командууд | [MANUAL_MN.md, Хэсэг 6](MANUAL_MN.md#part6) |
| Discord холболт | [MANUAL_MN.md, Хэсэг 7](MANUAL_MN.md#part7) |
| Өөр repo-ийн зүйл | доорх хүснэгт |

## Ангилал

| Ангилал | Юу | Хаана |
|---|---|---|
| **Нэгтгэсэн гарын авлага** | Суулгах, өдөр тутмын команд, plugin, admin, Discord, chat-ийн хэв маяг | [MANUAL_MN.md](MANUAL_MN.md) |
| **README-ууд (ангилсан)** | Таван repo ба plugin бүрийн README | [readmes/](readmes/README.md) |
| **Бусад бүх баримт (ангилсан)** | Deploy, API, rank, аудит, changelog, дизайн, upstream, repo бүрийн гарын авлага | [repo-docs/](repo-docs/README.md) |

## Таван repo

| Repo | Юу хийдэг | Бүртгэл | Гарын авлага |
|---|---|---|---|
| `legacyxxx-plugins` (энэ) | CS2 server plugin, `cs2-host.sh`, `announce.sh` | энэ файл | [MANUAL_MN.md](MANUAL_MN.md) |
| `legacyxxx-backend` | API, database, Steam нэвтрэлт | [docs/README.md](https://github.com/userneon/legacyxxx-backend/blob/main/docs/README.md) | [MANUAL_MN.md](https://github.com/userneon/legacyxxx-backend/blob/main/docs/MANUAL_MN.md) |
| `legacyxxx-frontend` | legacyx.cc вэб сайт | [docs/README.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/README.md) | [MANUAL_MN.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/MANUAL_MN.md) |
| `legacyxxx-discord-bot` | Discord bot | [docs/README.md](https://github.com/userneon/legacyxxx-discord-bot/blob/main/docs/README.md) | [MANUAL_MN.md](https://github.com/userneon/legacyxxx-discord-bot/blob/main/docs/MANUAL_MN.md) |
| `legacyxxx-workshop` | Тоглоом доторх HUD (Workshop addon) | [docs/README.md](https://github.com/userneon/legacyxxx-workshop/blob/main/docs/README.md) | [MANUAL_MN.md](https://github.com/userneon/legacyxxx-workshop/blob/main/docs/MANUAL_MN.md) |

## Дүрэм

- `readmes/` ба `repo-docs/` нь **хуулбар**. Жинхэнэ баримт нь эх repo дээр. Хуулбарыг гараар бүү засна.
- Шинэчлэх: таван repo нэг хавтсанд `git pull` хийсний дараа `python3 scripts/collect-docs.py`, дараа нь commit.
- Нууц (token, `API_SECRET`, RCON, Supabase service key) баримтад бичихгүй.
