#!/usr/bin/env node
/**
 * Builds the deployable LEGACY-X CS2 package: only what the game server needs at runtime.
 *
 *   node scripts/package-plugins.mjs [--out dist]
 *
 * Output (mirrors game/csgo/ on the server):
 *   dist/legacyx-cs2/addons/counterstrikesharp/plugins/<Plugin>/   plugin DLL, .deps.json, its own
 *                                                                   third-party DLLs, lang/gamedata
 *   dist/legacyx-cs2/addons/counterstrikesharp/shared/LegacyX.Shared.Configuration/
 *                                                                   the one shared config library
 *   dist/legacyx-cs2/addons/counterstrikesharp/.env.example
 *   dist/legacyx-cs2/addons/counterstrikesharp/gamedata/                  plugin signature files (weaponpaints.json)
 *   dist/legacyx-cs2/cfg/MatchZy/                                   MatchZy cfg files
 *   dist/legacyx-cs2/MANIFEST.sha256
 *
 * Never packaged: CounterStrikeSharp.API and every assembly of its dependency closure (the server's
 * CounterStrikeSharp install provides them), LegacyX.Shared.Configuration inside plugin folders
 * (loaded once from shared/), .pdb files,
 * tests, and native SQLite builds for platforms CS2 does not run on.
 */
import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const args = process.argv.slice(2)
const outIndex = args.indexOf('--out')
const outRoot = resolve(root, outIndex >= 0 ? args[outIndex + 1] : 'dist')
const packageRoot = join(outRoot, 'legacyx-cs2')
const cssRoot = join(packageRoot, 'addons', 'counterstrikesharp')
const stageRoot = join(outRoot, '.stage')

export const PLUGINS = ['LegacyX-Admin', 'LegacyX-AFKManager', 'LegacyX-Community', 'LegacyX-Hud', 'LegacyX-Killfeed', 'LegacyX-MatchZy', 'LegacyX-Spectator', 'LegacyX-Status', 'LegacyX-WeaponPaints']
const SHARED_LIBRARY = 'LegacyX.Shared.Configuration'
const NATIVE_RUNTIMES = new Set(['linux-x64', 'win-x64'])

/** Assembly names of CounterStrikeSharp.API and its whole dependency closure, from the project's restore graph. */
function counterStrikeSharpProvided(project) {
  const assets = JSON.parse(readFileSync(join(root, project, 'obj', 'project.assets.json'), 'utf8'))
  const target = Object.values(assets.targets)[0]
  const byName = new Map(Object.entries(target).map(([key, value]) => [key.split('/')[0].toLowerCase(), { key, ...value }]))
  const provided = new Set()
  const queue = ['counterstrikesharp.api']
  const seen = new Set()
  while (queue.length) {
    const name = queue.shift()
    if (seen.has(name)) continue
    seen.add(name)
    const library = byName.get(name)
    if (!library) continue
    const files = [...Object.keys(library.runtime ?? {}), ...Object.keys(library.compile ?? {})]
    for (const file of files) if (file.endsWith('.dll')) provided.add(file.split('/').pop().replace(/\.dll$/i, '').toLowerCase())
    for (const dependency of Object.keys(library.dependencies ?? {})) queue.push(dependency.toLowerCase())
  }
  return provided
}

function walk(directory) {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    return statSync(path).isDirectory() ? walk(path) : [path]
  })
}

function keep(relativePath, provided) {
  const parts = relativePath.split(sep)
  const file = parts.at(-1)
  if (file.endsWith('.pdb') || file.endsWith('.xml')) return false
  if (parts[0] === 'runtimes') return NATIVE_RUNTIMES.has(parts[1]) && parts[2] === 'native'
  if (file.endsWith('.dll')) {
    const name = file.replace(/\.dll$/i, '')
    if (name === SHARED_LIBRARY) return false
    if (provided.has(name.toLowerCase())) return false
  }
  return true
}

rmSync(packageRoot, { recursive: true, force: true })
rmSync(stageRoot, { recursive: true, force: true })
mkdirSync(cssRoot, { recursive: true })

const report = []
for (const plugin of PLUGINS) {
  const stage = join(stageRoot, plugin)
  execFileSync('dotnet', ['publish', join(root, plugin, `${plugin}.csproj`), '-c', 'Release', '-o', stage, '--nologo', '-v', 'q'], { stdio: 'inherit' })
  const provided = counterStrikeSharpProvided(plugin)
  const target = join(cssRoot, 'plugins', plugin)
  const kept = []
  const dropped = []
  for (const file of walk(stage)) {
    const path = relative(stage, file)
    if (keep(path, provided)) {
      mkdirSync(dirname(join(target, path)), { recursive: true })
      cpSync(file, join(target, path))
      kept.push(path)
    } else if (path.endsWith('.dll')) {
      dropped.push(path)
    }
  }
  if (!kept.includes(`${plugin}.dll`)) throw new Error(`${plugin}.dll missing from publish output`)
  report.push({ plugin, kept: kept.filter((path) => path.endsWith('.dll') || path.endsWith('.so')), dropped })
}

// The shared configuration library, once.
const sharedStage = join(stageRoot, 'LegacyX-Admin', `${SHARED_LIBRARY}.dll`)
mkdirSync(join(cssRoot, 'shared', SHARED_LIBRARY), { recursive: true })
cpSync(sharedStage, join(cssRoot, 'shared', SHARED_LIBRARY, `${SHARED_LIBRARY}.dll`))

cpSync(join(root, '.env.example'), join(cssRoot, '.env.example'))
// Signature files go where CounterStrikeSharp reads gamedata (addons/counterstrikesharp/gamedata/),
// not only next to the plugin: WeaponPaints refuses to load without it there.
for (const plugin of PLUGINS) {
  const gamedata = join(root, plugin, 'gamedata')
  if (!existsSync(gamedata)) continue
  for (const name of readdirSync(gamedata).filter((file) => file.endsWith('.json'))) {
    mkdirSync(join(cssRoot, 'gamedata'), { recursive: true })
    cpSync(join(gamedata, name), join(cssRoot, 'gamedata', name))
  }
}
cpSync(join(root, 'LegacyX-MatchZy', 'cfg', 'MatchZy'), join(packageRoot, 'cfg', 'MatchZy'), { recursive: true })
rmSync(stageRoot, { recursive: true, force: true })

// Guard: nothing CounterStrikeSharp provides, no duplicate shared library, no debug symbols.
const forbidden = new Set(PLUGINS.flatMap((plugin) => [...counterStrikeSharpProvided(plugin)]))
const violations = walk(packageRoot).filter((file) => {
  const name = file.split(sep).pop()
  if (name.endsWith('.pdb')) return true
  if (!name.endsWith('.dll') || file.includes(`${sep}runtimes${sep}`)) return false
  const assembly = name.replace(/\.dll$/i, '')
  if (forbidden.has(assembly.toLowerCase())) return true
  return assembly === SHARED_LIBRARY && !file.includes(`${sep}shared${sep}${SHARED_LIBRARY}${sep}`)
})
if (violations.length) {
  console.error(`FAIL: package contains files it must not ship:\n  ${violations.map((file) => relative(packageRoot, file)).join('\n  ')}`)
  process.exit(1)
}

const manifest = walk(packageRoot)
  .map((file) => `${createHash('sha256').update(readFileSync(file)).digest('hex')}  ${relative(packageRoot, file).split(sep).join('/')}`)
  .sort((a, b) => a.slice(66).localeCompare(b.slice(66)))
writeFileSync(join(packageRoot, 'MANIFEST.sha256'), manifest.join('\n') + '\n')

for (const { plugin, kept, dropped } of report) {
  console.log(`${plugin}: ${kept.join(', ')}${dropped.length ? `  (not packaged: ${dropped.join(', ')})` : ''}`)
}
console.log(`shared/${SHARED_LIBRARY}/${SHARED_LIBRARY}.dll`)
console.log(`PASS: ${manifest.length} files in ${relative(root, packageRoot) || packageRoot}; no CounterStrikeSharp-provided assemblies.`)
