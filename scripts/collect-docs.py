#!/usr/bin/env python3
"""
Refreshes the copies of every repo's documents under docs/ :

    python3 scripts/collect-docs.py [--repos <folder with the five legacyxxx-* clones>]

    docs/readmes/    the README of every repo and plugin, by category
    docs/repo-docs/  every other .md document of the five repos, by category

The copies carry a banner with their source and date; the real documents live in their own repos. Run this on a
machine that has all five clones (default: the folder next to this repository), pull them first, then commit.
Not copied: the Discord bot's faq/ and rules/ (content the bot shows, not documentation), vendored CounterStrikeSharp.
"""
import argparse, datetime, pathlib, re, shutil, sys

HERE = pathlib.Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('--repos', default=str(HERE.parent), help='folder holding legacyxxx-plugins, -backend, -frontend, -discord-bot, -workshop')
args = parser.parse_args()
H = pathlib.Path(args.repos)
today = datetime.date.today().isoformat()
GH = 'https://github.com/userneon'
for repo in ('plugins', 'backend', 'frontend', 'discord-bot', 'workshop'):
    if not (H / f'legacyxxx-{repo}').is_dir():
        sys.exit(f'legacyxxx-{repo} not found in {H}: pass --repos <folder>')


def banner(repo, path):
    return (f'> **Хуулбар.** Эх сурвалж: [{repo}/{path}]({GH}/{repo}/blob/main/{path}), {today}-д хуулсан. '
            'Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.\n\n')


# ---------------------------------------------------------------- README of every repo and plugin
OUT = HERE / 'docs' / 'readmes'
README_CATS = [
 ('1-game-server-plugins', 'Game server plugin-ууд (legacyxxx-plugins)', 'CS2 server дээр ажилладаг CounterStrikeSharp plugin-ууд: admin, цол, HUD, match, skin, төлөв.', [
   ('00-repo', 'legacyxxx-plugins', 'README.md', 'Репо: legacyxxx-plugins'),
   ('admin', 'legacyxxx-plugins', 'LegacyX-Admin/README.md', 'LegacyX-Admin'),
   ('afkmanager', 'legacyxxx-plugins', 'LegacyX-AFKManager/README.md', 'LegacyX-AFKManager'),
   ('community', 'legacyxxx-plugins', 'LegacyX-Community/README.md', 'LegacyX-Community'),
   ('hud', 'legacyxxx-plugins', 'LegacyX-Hud/README.md', 'LegacyX-Hud'),
   ('matchzy', 'legacyxxx-plugins', 'LegacyX-MatchZy/README.md', 'LegacyX-MatchZy'),
   ('spectator', 'legacyxxx-plugins', 'LegacyX-Spectator/README.md', 'LegacyX-Spectator'),
   ('status', 'legacyxxx-plugins', 'LegacyX-Status/README.md', 'LegacyX-Status'),
   ('weaponpaints', 'legacyxxx-plugins', 'LegacyX-WeaponPaints/README.md', 'LegacyX-WeaponPaints'),
   ('configs', 'legacyxxx-plugins', 'configs/README.md', 'configs/'),
   ('src', 'legacyxxx-plugins', 'src/README.md', 'src/'),
 ]),
 ('2-api-backend', 'API ба database (legacyxxx-backend)', 'api.legacyx.cc: API, database migration, Steam нэвтрэлт, plugin-ийн өгөгдөл хүлээн авалт.', [
   ('00-repo', 'legacyxxx-backend', 'README.md', 'Репо: legacyxxx-backend'),
 ]),
 ('3-website-frontend', 'Вэб сайт (legacyxxx-frontend)', 'legacyx.cc: React вэб сайт.', [
   ('00-repo', 'legacyxxx-frontend', 'README.md', 'Репо: legacyxxx-frontend'),
 ]),
 ('4-discord-bot', 'Discord bot (legacyxxx-discord-bot)', 'Welcome, дүрэм, FAQ, ticket, automod, шийтгэл, admin дуудлага, update-ийн мэдээ.', [
   ('00-repo', 'legacyxxx-discord-bot', 'README.md', 'Репо: legacyxxx-discord-bot'),
 ]),
 ('5-hud-workshop', 'Тоглоом доторх HUD (legacyxxx-workshop)', 'CS2 Workshop addon: Panorama layout, CSS, зураг.', [
   ('00-repo', 'legacyxxx-workshop', 'README.md', 'Репо: legacyxxx-workshop'),
 ]),
]

if OUT.exists(): shutil.rmtree(OUT)
OUT.mkdir(parents=True)
index = ['# Репо бүрийн README (ангилсан)\n',
         'Таван repo-ийн README-г ангилж энд цуглуулсан. **Эдгээр нь хуулбар (snapshot)**: жинхэнэ, хамгийн сүүлийн хувилбар нь тухайн repo дээр.\n',
         f'Сүүлд цуглуулсан: {today}. Бүх зүйлийг нэг дор уншихыг хүсвэл [MANUAL_MN.md](../MANUAL_MN.md).\n',
         '| Ангилал | Юу | README-ууд |', '|---|---|---|']
readme_count = 0
for d, title, desc, items in README_CATS:
    (OUT / d).mkdir()
    links = []
    for name, repo, path, label in items:
        text = (H / repo / path).read_text().replace('\r\n', '\n').rstrip('\n')
        (OUT / d / f'{name}.md').write_text(banner(repo, path) + text + '\n')
        links.append(f'[{label}]({d}/{name}.md)')
        readme_count += 1
    index.append(f'| **{title}** | {desc} | ' + ', '.join(links) + ' |')
(OUT / 'README.md').write_text('\n'.join(index) + '\n')

# ---------------------------------------------------------------- every other document
OUT = HERE / 'docs' / 'repo-docs'
CATS = {
 '01-deploy': ('Суулгах, deploy, ops', 'VPS, production deploy, зураг/asset түгээлт'),
 '02-api-security': ('API, хамгаалалт, гэрээ', 'API лавлах, аюулгүй байдал, feature flag, plugin ↔ API гэрээ'),
 '03-rank-match': ('Цол, EXP, match', 'Rank систем, сарын reset, leaderboard, match систем, telemetry'),
 '04-admin-staff': ('Admin, staff, эрх', 'AdminPlus, staff panel, role шилжилт'),
 '05-skin-hud': ('Skin ба HUD', 'Skinchanger runbook, Workshop HUD гэрээ ба гарын авлага'),
 '06-audits': ('Аудит, шалгалт', 'Тодорхой огнооны аудит, production шалгалт'),
 '07-changelogs': ('Changelog-ууд', 'Plugin, rank, reconnect-ийн өөрчлөлтийн түүх'),
 '08-frontend-design': ('Вэб дизайн ба ажлын дүрэм', 'Frontend-ийн дизайн, token, зураг, `CLAUDE.md`'),
 '09-other': ('Бусад', 'Context, todo, promotion code, reconnect, Steam background'),
 '10-upstream': ('Upstream (гадны) баримт', 'MatchZy болон бусад гадны төслийн баримт: LEGACY-X-ийнх биш'),
}
MAP = {}
def put(cat, repo, paths):
    for p in paths: MAP[(repo, p)] = cat
B, F, W, P = 'legacyxxx-backend', 'legacyxxx-frontend', 'legacyxxx-workshop', 'legacyxxx-plugins'
put('01-deploy', B, ['VPS_DEPLOY.md', 'docs/PRODUCTION_DEPLOYMENT.md'])
put('01-deploy', F, ['docs/IMAGE_DELIVERY.md'])
put('02-api-security', B, ['API.md', 'docs/API_SECURITY_HARDENING.md', 'docs/FEATURE_FLAGS.md', 'docs/FRONTEND_ENDPOINT_ADAPTER_SPEC.md', 'docs/LEGACY_RLS_HARDENING_2026-08-24.md', 'docs/PLUGIN_READY_CONTRACT_V1.md'])
put('03-rank-match', B, ['docs/RANK_SYSTEM.md', 'docs/MONTHLY_RANK_RESET.md', 'docs/LEADERBOARD_RANK_INTEGRATION.md', 'docs/COMMUNITY_PROGRESSION_CLANS.md', 'docs/PLUGIN_RANKED_TELEMETRY_V2.md', 'docs/UNIFIED_MATCH_SYSTEM_RUNBOOK.md'])
put('03-rank-match', P, ['LegacyX-MatchZy/RANK_BRIDGE.md'])
put('04-admin-staff', B, ['docs/ADMINPLUS_API_ONLY.md', 'docs/ADMINPLUS_PRODUCTION_SETUP.md', 'docs/STAFF_PANEL.md'])
put('05-skin-hud', B, ['docs/SKINCHANGER_OPERATOR_RUNBOOK.md', 'docs/SKINCHANGER_STATIC_ASSET_HOSTING.md'])
put('05-skin-hud', W, ['CONTRACT.md', 'docs/GARIIN_AVLAGA_MN.md'])
put('06-audits', B, ['AUDIT_2026-09-20.md', 'docs/PLUGIN_GAP_AUDIT_2026-08-24.md', 'docs/PRODUCTION_DB_AUDIT.md', 'docs/RANK_EXP_AUDIT_2026-08-24.md', 'docs/ROLE_MIGRATION_AUDIT.md', 'docs/USER_ROLE_MIGRATION_AUDIT_2026-08-24.md', 'docs/SERVER_LIVE_MATCH_AUDIT_2026-08-24.md', 'docs/UNIFIED_MATCH_SYSTEM_AUDIT.md', 'docs/V1_CLEANUP_AUDIT.md'])
put('07-changelogs', B, ['docs/ADMINPLUS_LEGACYX_CHANGELOG.md', 'docs/COMMUNITY_PROGRESSION_CHANGELOG.md', 'docs/MONTHLY_RANK_RESET_CHANGELOG.md', 'docs/RANK_ADMINPLUS_CHANGELOG.md', 'docs/RECONNECT_CHANGELOG.md'])
put('07-changelogs', P, ['AFKMANAGER_LEGACYX_CHANGELOG.md', 'COMMUNITY_LEGACYX_CHANGELOG.md', 'MATCHZY_LEGACYX_CHANGELOG.md', 'SPECTATOR_COMMS_LEGACYX_CHANGELOG.md', 'LegacyX-MatchZy/CHANGELOG.md'])
put('08-frontend-design', F, ['CLAUDE.md', 'docs/design/README.md', 'docs/design/PROMPT.md', 'docs/design/RANK-SYSTEM.md'])
put('09-other', B, ['MASTER_CONTEXT.md', 'todo.md', 'docs/PROMOTION_CODES.md', 'docs/RECONNECT_LAST_PLAYED.md', 'docs/STEAM_PROFILE_BACKGROUND_FEASIBILITY.md'])
put('10-upstream', P, ['README.upstream.md', 'LegacyX-MatchZy/README.upstream.md', 'LegacyX-WeaponPaints/UPSTREAM.md'])
mz = H / P / 'LegacyX-MatchZy/documentation/docs'
for f in sorted(mz.glob('*.md')): MAP[(P, f'LegacyX-MatchZy/documentation/docs/{f.name}')] = '10-upstream'


if OUT.exists(): shutil.rmtree(OUT)
OUT.mkdir(parents=True)
index = {c: [] for c in CATS}
missing = []
for (repo, path), cat in sorted(MAP.items(), key=lambda kv: (kv[1], kv[0][0], kv[0][1])):
    src = H / repo / path
    if not src.exists():
        missing.append(f'{repo}/{path}')
        continue
    flat = re.sub(r'[^A-Za-z0-9._-]+', '_', path.replace('/', '__'))
    name = f'{repo.replace("legacyxxx-", "")}--{flat}'
    (OUT / cat).mkdir(exist_ok=True)
    text = src.read_text().replace('\r\n', '\n').rstrip('\n')
    (OUT / cat / name).write_text(banner(repo, path) + text + '\n')
    first = next((l[2:].strip() for l in text.split('\n') if l.startswith('# ')), path)
    index[cat].append((f'{cat}/{name}', repo, path, first, text.count('\n') + 1))
lines = ['# Бүх repo-ийн баримт (ангилсан)\n',
         'Таван repo-ийн README-ээс бусад бүх баримтыг (`.md`) ангилж цуглуулсан. **Хуулбар (snapshot)**: жинхэнэ хувилбар нь эх repo дээр.\n',
         f'Сүүлд цуглуулсан: {today}. README-ууд: [readmes/](../readmes/README.md). Нэгтгэсэн гарын авлага: [MANUAL_MN.md](../MANUAL_MN.md).\n',
         'Оруулаагүй: Discord bot-ын `faq/*.md`, `rules/*.md` (эдгээр нь bot-ын ажиллах агуулга, баримт биш), CounterStrikeSharp-ийн гадны файл.\n']
doc_count = 0
for c, (title, desc) in CATS.items():
    if not index[c]: continue
    lines += [f'## {title}', '', desc, '', '| Баримт | Repo | Мөр |', '|---|---|---|']
    for rel, repo, path, first, n in index[c]:
        lines.append(f'| [{first}]({rel}) | `{repo.replace("legacyxxx-", "")}/{path}` | {n} |')
        doc_count += 1
    lines.append('')
(OUT / 'README.md').write_text('\n'.join(lines) + '\n')

print(f'{readme_count} READMEs and {doc_count} documents copied from {H}')
if missing:
    print('NOT FOUND (renamed or removed in its repo? update the lists in this script):', *missing, sep='\n  ')

# A document that is new in a repo and in none of the lists above would silently be left out: say so.
known = {p for (_, p) in MAP} | {p for _, items in [(c[0], c[3]) for c in README_CATS] for (_, r, p, _) in items}
for repo in ('plugins', 'backend', 'frontend', 'discord-bot', 'workshop'):
    root = H / f'legacyxxx-{repo}'
    import subprocess
    files = subprocess.run(['git', '-C', str(root), 'ls-files', '*.md'], capture_output=True, text=True).stdout.split()
    new = [f for f in files if f not in known and not f.startswith(('CounterStrikeSharp/', 'faq/', 'rules/', 'docs/readmes/', 'docs/repo-docs/'))
           and f not in ('docs/MANUAL_MN.md', 'ADMINPLUS_DISCORD_CONNECTION.md') and not f.endswith('README.md')]
    if new:
        print(f'NOT IN ANY LIST in legacyxxx-{repo}:', *new, sep='\n  ')
