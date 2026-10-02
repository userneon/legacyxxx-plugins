> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/docs/MANUAL_MN.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/MANUAL_MN.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# legacyxxx-frontend: гарын авлага

`legacyx.cc`-ийн вэб сайт: React 19, TypeScript, Vite, Tailwind v4, shadcn/ui (Radix). Статик bundle болж Nginx-ээр үйлчилнэ.

| Хэсэг | Агуулга |
|---|---|
| [1. Энэ repo юу вэ](#1-энэ-repo-юу-вэ) | Үүрэг, аюулгүй байдлын хил |
| [2. Локал дээр ажиллуулах](#2-локал-дээр-ажиллуулах) | Суулгах, mock горим, шалгалт |
| [3. Хуудас ба route](#3-хуудас-ба-route) | Хуудас бүр юу |
| [4. Бүтэц](#4-бүтэц) | Хавтас бүр юу |
| [5. Тохиргоо](#5-тохиргоо) | `VITE_*`, feature switch |
| [6. API-тай холбогдох](#6-api-тай-холбогдох) | Client, нэвтрэлт, өгөгдлийн дүрэм |
| [7. Дизайн систем](#7-дизайн-систем) | Өнгө, радиус, текст, товч, glass |
| [8. Зураг ба asset](#8-зураг-ба-asset) | WebP, icon, rank |
| [9. Build ба deploy](#9-build-ба-deploy) | VPS, Nginx |
| [10. Шинэ зүйл нэмэх](#10-шинэ-зүйл-нэмэх) | Хуудас, API, товч |
| [11. Алдаа засах](#11-алдаа-засах) | Шинж тэмдэг → шалтгаан |
| [12. Хуучирсан зүйлс](#12-хуучирсан-зүйлс) | Баримт дахь зөрүү |
| [13. Бусад баримт](#13-бусад-баримт) | [README.md](README.md) индекс |

---

## 1. Энэ repo юу вэ

- Тоглогчдод харагдах бүх зүйл: нүүр, Play (5x5 / Fun / Pro League), Leaders, Profile, Skinchanger, Penalties, Reviews, Explore, Tournaments, Settings.
- Өгөгдөл **зөвхөн бодит API-аас** (`api.legacyx.cc`). Зохиомол тоо, нэр, жагсаалт харуулахгүй: ачаалж байгаа үед skeleton,
  хоосон үед богино саарал мессеж, алдаа гарвал нэг мөр + Retry.
- **Аюулгүй байдлын хил:** браузер зөвхөн нийтийн, rate limit-тэй read endpoint-ууд болон өөрийн session-оор хандах хэрэглэгчийн endpoint-уудыг дууддаг.
  Оператор API, plugin token, `API_SECRET`, RCON, Supabase service-role нууц frontend-д (код, `.env`) **хэзээ ч** орохгүй.
- Хэрэглэгчийн бичсэн текст (нэр, review, шийтгэлийн шалтгаан) үргэлж escape хийгдэж харагдана, түүхий HTML хэзээ ч биш.

## 2. Локал дээр ажиллуулах

Node 22 (багадаа 20.19), npm.

```bash
npm ci                          # lockfile-оор суулгана
npm run dev                     # VITE_API_URL байхгүй бол mock өгөгдөлтэй (localhost:5173)
VITE_API_URL=https://api.legacyx.cc npm run dev    # бодит API-тай
npm run dev:mock                # mock-ыг шууд заана (VITE_MOCK_API=1)
npm run preview:mock            # mock-той production-ийн урьдчилсан үзүүлэлт
npm run typecheck               # tsc --noEmit
npm run check:colors            # өнгөний шалгагч: commit-ийн өмнө заавал
npm run build                   # tsc -b && vite build → dist/
```

- **Mock горим** (`src/api/mock.ts`): `npm run dev` дээр `VITE_API_URL`-гүй бол автоматаар асна. Нэвтэрсэн тоглогчийг
  `localStorage`-д `legacyx_access_token=mock-session` тавьж дуурайна. Зөвхөн дизайн харахад; production build-д mock код **орохгүй**
  (deploy script `mock-session` олдвол зогсооно).
- Mock өгөгдөл нь бодит биш: дизайн харахад л хэрэглэ, "ийм тоо байна" гэж баримт болгохгүй.

## 3. Хуудас ба route

| Хуудас | Route | Тэмдэглэл |
|---|---|---|
| Home | `/` | Hero, mode карт (hover-д өргөсөх), Top players, Reviews, Discord |
| 5x5 Matches | `/play/5x5` | Server карт: 10 слот T/CT өнгөөр, Details, Quick join |
| Fun Mode | `/play/fun` | Capacity bar |
| Pro League | `/play/pro` | Rank шаардлагатай, түгжээтэй хуудас |
| Tournaments | `/tournaments` | Бүртгэл, bracket |
| Leaders | `/leaders` | EXP / K/D / Win rate, top-3 |
| Profile | `/profile`, `/profile/:id` | Өөрийн болон бусдын; EXP хязгаар, match |
| Skinchanger | `/skinchanger` | T/CT, skin сонгогч, sticker, customize |
| Penalties | `/penalties` | Шийтгэл, хайлт, дэлгэрэнгүй |
| Reviews | `/reviews` | Review бичих, шүүх |
| Explore | `/explore` | Тоглогч хайх |
| Settings | `/settings` | Холболт, мэдэгдэл, вэб тохиргоо |
| Connect | `/connect?server=<id>` | Server рүү орох (Join, Copy IP, тоглогчид). Discord товч энд ирдэг |
| Staff panel | `/staffpanel` | Зөвхөн OWNER/MANAGER (backend шалгана) |
| Clan | `/clan` | Унтраалттай (`FEATURES.clan = false`) |

Route-ийн жагсаалт: `src/lib/routes.ts` (хуучин хаягууд `/play/5vs5`, `/feedback` ажиллана). Нэр нь sidebar, гарчиг, tab-д нэг.

## 4. Бүтэц

| Зам | Юу |
|---|---|
| `src/main.tsx`, `src/App.tsx` | Эхлэл, router, shell |
| `src/pages/*.tsx` | Хуудас бүр |
| `src/components/` | Дахин ашиглагдах хэсгүүд (`app-sidebar`, `player-avatar`, `kill-feed`, `background-beams`, `skeletons`…) |
| `src/components/ui/` | shadcn/Radix суурь (button, dialog, sheet, tabs…) |
| `src/api/` | API client (`client.ts`), хэсэг бүрийн service, төрлүүд (`types.ts`), mock (`mock.ts`) |
| `src/hooks/` | `use-api-query` (өгөгдөл татах), `use-auth`, `use-mobile` |
| `src/lib/` | `routes.ts`, `features.ts`, `links.ts`, `cs2-map-art.ts`, `cs2-rarity.ts` |
| `src/index.css` | Бүх token, glass, animation (`--brand`, `--glass-fill`…) |
| `src/assets/` | Bundle-д орох зураг (map, skinchanger, rank) |
| `public/` | Нэрлэсэн статик: `ranks/`, `logolegacyx.webp`, `_redirects` (SPA fallback) |
| `scripts/` | `check-no-blue.mjs` (`check:colors`), зураг WebP болгох |
| `ops/deploy.sh` | VPS дээр нэг командаар deploy |
| `docs/design/` | Дэлгэц бүрийн HTML+PNG лавлах |

## 5. Тохиргоо

`VITE_*` утгууд build үед bundle-д шингэнэ (нууц ОРУУЛАХГҮЙ).

| Хувьсагч | Анхдагч | Тайлбар |
|---|---|---|
| `VITE_API_URL` | production build-д `https://api.legacyx.cc` | API-ийн origin |
| `VITE_MOCK_API` | — | `1` бол mock |
| `VITE_DISCORD_INVITE_URL` | `https://discord.gg/legacyx` | Discord урилга |
| `VITE_DISCORD_APPEALS_URL`, `…_REPORTS_URL`, `…_ANNOUNCEMENTS_URL`, `…_STAFF_URL` | урилга | Тодорхой channel-ийн холбоос |
| `VITE_SERVER_RULES_URL`, `VITE_TOURNAMENT_RULES_URL` | байхгүй | Дүрмийн холбоос (байхгүй бол товч гарахгүй) |

**Feature switch** (`src/lib/features.ts`): унтраалттай feature нь nav, товч, карт, route-д огт харагдахгүй (placeholder, "Coming soon" байхгүй).
Одоо унтраалттай: `clan`, `roster`. Асаахдаа тэнд `true` болгоно. Backend-ийн `GET /public/features` тусдаа (backend-ийн flag-ууд).

## 6. API-тай холбогдох

- Бүх хүсэлт `src/api/client.ts`-ээр: `credentials: "include"`, 15 секундын timeout, алдааг `ApiError` болгоно.
- Нэвтрэлт: Steam OpenID (backend). Session нь `.legacyx.cc` HTTP-only cookie; `legacyx_access_token` нь `localStorage`-д.
- Өгөгдөл татахдаа `useApiQuery(...)`: `loading`, `error`, `refetch`, `keepPreviousData`. Хуудас бүр дөрвөн төлөвийг зохицуулна:
  ачаалж байна (skeleton), хоосон, алдаа (Retry), өгөгдөлтэй.
- Шинэ endpoint нэмэхдээ `src/api/<хэсэг>.ts` дотор service, `types.ts` дотор төрөл, `mock.ts` дотор mock хариу нэмнэ.
- Шинэ талбар backend-д байхгүй бол UI-д зохиож харуулахгүй; талбарыг nullable болгож, байхгүй үед юу ч харуулахгүй/зөв хоосон мессеж.

## 7. Дизайн систем

Бүрэн дүрэм: **[../CLAUDE.md](../CLAUDE.md)** (эх сурвалж). Товчоор:

- **Өнгө:** саарал/хар гадаргуу, цагаан гол accent (`--accent-solid`), **crimson** (`--brand` `#e11d48`, `--brand-bright` `#ff3d6e`) цөөн чухал газарт: wordmark-ийн "-X", идэвхтэй nav, #1 болон өөрийн мөр, Pro League карт, hero-ийн онцлох үг, EXP progress. Бусад нь саарал.
- **Статус өнгө:** ногоон (live/online), улаан `#ef4444` (EXP хязгаар, ban). Цолны өнгө зөвхөн emblem болон цолны нэрийн хажууд.
- **Радиус:** 8px товч/талбар, 10–12px карт, 14–16px floating, 999px pill.
- **Текст:** Onest. 10–11 meta, 13 body, 15 онцлол, 22+ гарчиг. `tabular-nums` хэрэглэхгүй.
- **Товч:** primary = цагаан (`.lx-primary-button`); secondary = хүрээтэй `--raised`; hover саарал. Crimson товч зөвхөн идэвхтэй төлөвт.
- **Гадаргуу:** glass (`--glass-fill`, `.glass`, `.lx-glass`), shell нь `.lx-glass-shell` (бүдгэрсэн). Зурагны дээрх overlay нь тод.
- **Icon:** зөвхөн `lucide-react`, stroke 2. Emoji, placeholder icon байхгүй.
- **Хөдөлгөөн:** `prefers-reduced-motion`-ийг хүндэтгэнэ. Background beams нь scroll хийхэд зогсож, 15 frame/сек (гүйцэтгэл).
- **Шалгагч:** `npm run check:colors` цэнхэр/нил/шар/улбар шар hue-г (`palette-exempt`/`rarity`-аас бусад) зөвшөөрөхгүй. Commit-ийн өмнө заавал.
- Дэлгэц бүрийн лавлах: [design/README.md](design/README.md) (HTML + PNG).

## 8. Зураг ба asset

- Зураг WebP; `OptimizedImage` нь өргөн/өндөр, lazy, async decode, алдааны fallback өгнө. Дэлгэрэнгүй: [IMAGE_DELIVERY.md](IMAGE_DELIVERY.md).
- Map: `src/assets/maps/de_*.webp`, `src/lib/cs2-map-art.ts`. Цол: `public/ranks/` (18). Skin: `src/assets/skinchanger/`.
- Шинэ PNG/JPG нэмсэн бол `npm run images:webp`.
- CS2 rarity өнгө (`cs2-rarity.ts`) зөвхөн skin tile-ийн нимгэн заагч, дэвсгэр биш.

## 9. Build ба deploy

VPS дээр:

```bash
cd ~/legacyxxx-frontend
bash ops/deploy.sh
```

Скрипт: `git pull --ff-only main` → `npm ci` → `VITE_API_URL=<API> npm run build` → bundle-д `mock-session` байвал **зогсоно** →
`/var/www/legacyx` руу хуулна (эхлээд hash-тай `assets/`, **сүүлд** `index.html`, хуучин asset-ыг цэвэрлэнэ) → `nginx -t` → reload →
Discord-д "Website updated" мэдэгдэл.

Өөрчлөх: `WEB_ROOT=/var/www/legacyx API_URL=https://api.legacyx.cc BRANCH=main bash ops/deploy.sh`.

Nginx: `root /var/www/legacyx`, SPA fallback `try_files $uri /index.html` (эсвэл `_redirects`), `/assets/` дээр урт `immutable` cache.
`api.legacyx.cc` нь тусдаа (backend).

## 10. Шинэ зүйл нэмэх

**Хуудас:** `src/pages/<нэр>.tsx` → `src/api/types.ts`-ийн `PageId` → `src/lib/routes.ts`-ийн `PAGE_ROUTES`/`PAGE_TITLES` → sidebar → (унтраалттай эхлэх бол `features.ts`).
**API дуудлага:** service → `useApiQuery` → дөрвөн төлөв → mock хариу.
**Өнгө/товч:** CLAUDE.md-ийн жагсаалтыг шалга; шинэ crimson газар нэмэхээс өмнө "чухал газар мөн үү?" гэж асуу; үгүй бол саарал.
**Commit:** main дээр шууд, нэг логик өөрчлөлт = нэг commit. Commit-ийн нэр нь Discord-д олон нийтэд харагдана (нэг энгийн өгүүлбэр); нууц/аюулгүй байдлын засвар бол `[skip announce]`.

## 11. Алдаа засах

| Шинж тэмдэг | Шалтгаан | Засах |
|---|---|---|
| Хуудас хоосон, "Retry" | API хүрэхгүй / CORS | `VITE_API_URL`; API-ийн `FRONTEND_ORIGIN`; DevTools → Network |
| Нэвтэрсэн ч нэвтрээгүй харагдана | Cookie домэйн, `credentials` | API `AUTH_COOKIE_DOMAIN=.legacyx.cc`; HTTPS |
| `mock-session` олдлоо, deploy зогсов | Mock-той build хийсэн | `VITE_MOCK_API`-г арилгаж дахин build |
| `check:colors` унана | Хориотой hue (цэнхэр/шар…) | Token (`var(--…)`) ашигла, шаардлагатай бол `palette-exempt` |
| Шинэ дизайн харагдахгүй (deploy-ийн дараа) | Browser cache | `Ctrl+Shift+R`; `index.html` шинэчлэгдсэн эсэх |
| Зураг гарахгүй | Зам, WebP дутуу | `npm run images:webp`; `src/assets` импорт |
| Удаан, гацна | Beams/blur | `background-beams.tsx`, `index.css`-ийн blur; DevTools Performance |
| `tsc` алдаа | Төрөл зөрсөн | `npm run typecheck` |

## 12. Хуучирсан зүйлс

- `README.md` (язгуур): `/api/public/*`, `FRONTEND_PUBLIC_ORIGINS` гэж бичсэн; одоогийн API нь `/api/v1/*`, CORS нь backend-ийн `FRONTEND_ORIGIN`. §6-г ашигла.
- `docs/IMAGE_DELIVERY.md`: "local Vite preview" болон `/home/ubuntu/webdev-static-assets` зам Manus-ийн орчинд зориулагдсан.
- `docs/design/PROMPT.md` §2: crimson-ийн өнөөгийн хэрэглээг `CLAUDE.md` илүү нарийн заана.

## 13. Бусад баримт

Бүх баримтын ангилсан индекс: **[README.md](README.md)**.
