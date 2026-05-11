import Database from 'better-sqlite3';
import * as fs from 'node:fs';
import * as path from 'node:path';

// Resolve relative to the Playwright working directory (which is the e2e/
// folder when invoked via npm scripts). Override with E2E_CONFIG_DIR if the
// suite is run from elsewhere.
export const CONFIG_DIR =
  process.env.E2E_CONFIG_DIR ?? path.resolve(process.cwd(), '.e2e-config');

const DATA_DB = path.join(CONFIG_DIR, 'cleanuparr.db');
const EVENTS_DB = path.join(CONFIG_DIR, 'events.db');
const USERS_DB = path.join(CONFIG_DIR, 'users.db');

const EVENTS_TABLES = [
  'search_event_data',
  'events',
  'manual_events',
  'strikes',
  'download_items',
  'job_runs',
];

// Dynamic tables only — singleton configs (general_configs, queue_cleaner_configs,
// arr_configs, etc.) are intentionally left alone so each test starts with the
// default config the backend seeds via migrations.
const DATA_TABLES_DYNAMIC = [
  'seeker_history',
  'custom_format_score_history',
  'custom_format_score_entries',
  'seeker_command_trackers',
  'search_queue',
  'seeker_instance_configs',
  'arr_instances',
  'stall_rules',
  'slow_rules',
  'q_bit_seeding_rules',
  'deluge_seeding_rules',
  'transmission_seeding_rules',
  'u_torrent_seeding_rules',
  'r_torrent_seeding_rules',
  'unlinked_configs',
  'blacklist_sync_history',
  'download_clients',
  'notifiarr_configs',
  'apprise_configs',
  'ntfy_configs',
  'pushover_configs',
  'telegram_configs',
  'discord_configs',
  'gotify_configs',
  'notification_configs',
];

export interface ResetCounts {
  events: number;
  data: number;
  usersUnlocked: number;
  refreshTokens: number;
}

function openDb(filePath: string): Database.Database {
  const db = new Database(filePath, { fileMustExist: true });
  db.pragma('journal_mode = WAL');
  db.pragma('busy_timeout = 5000');
  db.pragma('foreign_keys = ON');
  return db;
}

function deleteFromTables(db: Database.Database, tables: readonly string[]): number {
  let total = 0;
  const tx = db.transaction(() => {
    for (const table of tables) {
      try {
        const info = db.prepare(`DELETE FROM ${table}`).run();
        total += info.changes;
      } catch (e) {
        // Table doesn't exist in the current schema — silently skip. The
        // migrations rename / drop tables over time and the test reset module
        // shouldn't fail just because one of those names is no longer present.
        const code = (e as { code?: string }).code;
        if (code !== 'SQLITE_ERROR') {
          throw e;
        }
      }
    }
  });
  tx();
  return total;
}

export function resetDatabases(): ResetCounts {
  const eventsDb = openDb(EVENTS_DB);
  let events = 0;
  try {
    events = deleteFromTables(eventsDb, EVENTS_TABLES);
  } finally {
    eventsDb.close();
  }

  const dataDb = openDb(DATA_DB);
  let data = 0;
  try {
    data = deleteFromTables(dataDb, DATA_TABLES_DYNAMIC);
  } finally {
    dataDb.close();
  }

  const usersDb = openDb(USERS_DB);
  let usersUnlocked = 0;
  let refreshTokens = 0;
  try {
    const tx = usersDb.transaction(() => {
      refreshTokens = usersDb.prepare('DELETE FROM refresh_tokens').run().changes;
      usersUnlocked = usersDb
        .prepare('UPDATE users SET failed_login_attempts = 0, lockout_end = NULL')
        .run().changes;
    });
    tx();
  } finally {
    usersDb.close();
  }

  return { events, data, usersUnlocked, refreshTokens };
}

export async function waitForDatabases(timeoutMs = 90_000): Promise<void> {
  const start = Date.now();
  const files = [DATA_DB, EVENTS_DB, USERS_DB];
  while (Date.now() - start < timeoutMs) {
    if (files.every((f) => existsAndNonEmpty(f))) {
      return;
    }
    await new Promise((r) => setTimeout(r, 250));
  }
  throw new Error(
    `Cleanuparr SQLite databases not ready within ${timeoutMs}ms. Looked at:\n  ${files.join('\n  ')}\nIs the app container running and is ./.e2e-config mounted to /config?`,
  );
}

function existsAndNonEmpty(filePath: string): boolean {
  try {
    return fs.statSync(filePath).size > 0;
  } catch {
    return false;
  }
}
