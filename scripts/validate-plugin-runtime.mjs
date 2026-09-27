#!/usr/bin/env node
/**
 * Validates a host-local CounterStrikeSharp/.env without printing secrets.
 * Usage: node scripts/validate-plugin-runtime.mjs --env /path/to/CounterStrikeSharp/.env
 */
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const args = process.argv.slice(2)
const index = args.indexOf('--env')
const envPath = resolve(index >= 0 && args[index + 1] ? args[index + 1] : 'CounterStrikeSharp/.env')
if (!existsSync(envPath)) {
  console.error(`FAIL: environment file not found: ${envPath}`)
  process.exit(1)
}

const values = new Map()
for (const raw of readFileSync(envPath, 'utf8').split(/\r?\n/)) {
  const line = raw.trim()
  if (!line || line.startsWith('#')) continue
  const separator = line.indexOf('=')
  if (separator <= 0) continue
  values.set(line.slice(0, separator).trim(), line.slice(separator + 1).trim())
}

const missing = []
const invalid = []
const stale = []
const requireValue = (key, predicate = (value) => value.length > 0) => {
  const value = values.get(key) ?? ''
  if (!predicate(value)) missing.push(key)
}
const enabled = (module) => (values.get(`LEGACYX_${module}_ENABLED`) ?? '').toLowerCase() === 'true'

for (const key of ['LEGACYX_API_BASE_URL', 'LEGACYX_SERVER_ID', 'LEGACYX_SERVER_ADDRESS', 'LEGACYX_SERVER_MODE']) requireValue(key)
if (!/^https:\/\/[^/]+/i.test(values.get('LEGACYX_API_BASE_URL') ?? '')) invalid.push('LEGACYX_API_BASE_URL must be an HTTPS Root API URL')

for (const module of ['COMMUNITY']) {
  if (enabled(module)) {
    requireValue(`LEGACYX_${module}_PLUGIN_ID`)
    requireValue(`LEGACYX_${module}_PLUGIN_TOKEN`, (value) => value.length >= 24)
  }
}
if (enabled('SKINBRIDGE')) {
  requireValue('LEGACYX_SKINBRIDGE_PLUGIN_ID')
  requireValue('LEGACYX_SKINBRIDGE_PLUGIN_TOKEN', (value) => value.length >= 24)
  const poll = Number(values.get('LEGACYX_SKINBRIDGE_POLL_SECONDS'))
  if (!Number.isInteger(poll) || poll < 1 || poll > 30) invalid.push('LEGACYX_SKINBRIDGE_POLL_SECONDS must be an integer from 1 to 30')
}
if ((values.get('LEGACYX_MATCHZY_MATCH_CORE_ENABLED') ?? '').toLowerCase() === 'true') {
  for (const key of ['LEGACYX_MATCHZY_MATCH_CORE_API_URL', 'LEGACYX_MATCHZY_MATCH_CORE_PLUGIN_ID']) requireValue(key)
  requireValue('LEGACYX_MATCHZY_MATCH_CORE_PLUGIN_TOKEN', (value) => value.length >= 24)
}
if (enabled('ADMIN')) {
  requireValue('LEGACYX_ADMIN_API_BASE_URL', (value) => /^https:\/\/[^/]+/i.test(value))
  requireValue('LEGACYX_ADMIN_PLUGIN_ID')
  requireValue('LEGACYX_ADMIN_PLUGIN_SECRET', (value) => value.length >= 24)
  const refresh = Number(values.get('LEGACYX_ADMIN_AUTH_REFRESH_SECONDS') ?? 60)
  const cache = Number(values.get('LEGACYX_ADMIN_AUTH_CACHE_SECONDS') ?? 180)
  if (!Number.isInteger(refresh) || refresh < 15 || refresh > 600) invalid.push('LEGACYX_ADMIN_AUTH_REFRESH_SECONDS must be an integer from 15 to 600')
  if (!Number.isInteger(cache) || cache < 30 || cache > 3600 || cache <= refresh) invalid.push('LEGACYX_ADMIN_AUTH_CACHE_SECONDS must be an integer from 30 to 3600 and above the refresh interval')
}
if ((values.get('LEGACYX_ADMIN_CALL_CHANNEL_ENABLED') ?? '').toLowerCase() === 'true') {
  requireValue('LEGACYX_ADMIN_CALL_CHANNEL_WEBHOOK', (value) => /^https:\/\/(discord\.com|discordapp\.com)\/api\/webhooks\//i.test(value))
}

const rconValues = ['LEGACYX_RCON_HOST', 'LEGACYX_RCON_PORT', 'LEGACYX_RCON_PASSWORD']
const rconPresent = rconValues.some((key) => (values.get(key) ?? '').length > 0)
if (rconPresent) {
  for (const key of rconValues) requireValue(key)
  const port = Number(values.get('LEGACYX_RCON_PORT'))
  if (!Number.isInteger(port) || port < 1 || port > 65535) invalid.push('LEGACYX_RCON_PORT must be 1–65535')
  if ((values.get('LEGACYX_RCON_PASSWORD') ?? '').length < 16) invalid.push('LEGACYX_RCON_PASSWORD must contain at least 16 characters')
}

for (const forbidden of ['SUPABASE_URL', 'SUPABASE_SERVICE_ROLE_KEY', 'DATABASE_URL', 'STEAM_API_KEY']) {
  if ((values.get(forbidden) ?? '').length > 0) invalid.push(`${forbidden} must not be present in the plugin environment`)
}
for (const key of values.keys()) {
  if (key.startsWith('LEGACYX_ADMIN_DISCORD_') || key.startsWith('LEGACYX_ADMIN_POLICY_')) stale.push(key)
  if (/^LEGACYX_(RECONNECT|PLAYER_TELEMETRY|PHANTOM)_/.test(key)) stale.push(key)
}

console.log(`Plugin runtime validation: ${envPath}`)
console.log(`Shared identity: ${missing.length === 0 ? 'checked' : 'incomplete'}`)
if (missing.length) console.error(`MISSING: ${missing.join(', ')}`)
if (invalid.length) console.error(`INVALID: ${invalid.join('; ')}`)
if (stale.length) console.error(`REMOVE STALE SETTINGS: ${stale.join(', ')}`)
if (missing.length || invalid.length || stale.length) process.exit(1)
console.log('PASS: central plugin environment is structurally ready; no secret values were printed.')
