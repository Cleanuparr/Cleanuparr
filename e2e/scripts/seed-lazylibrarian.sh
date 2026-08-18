#!/usr/bin/env bash
#
# Rebuild e2e/lazylibrarian-seed from scratch.
#
# Boots LazyLibrarian empty, writes its config.ini, adds one book per spec.
# Snapshots /config into e2e/lazylibrarian-seed, which the e2e run restores.
# CI therefore needs no network and every run sees the same library.
#
# Needs internet: LazyLibrarian reads OpenLibrary.
# Stops the lazylibrarian container.
#
# Re-run it when the pinned image tag changes, or when the library has to change.
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DATA="$HERE/test-data"
SEED="$HERE/lazylibrarian-seed"
COMPOSE="docker compose -f $HERE/docker-compose.e2e.yml"

URL="http://localhost:5299"
API_KEY="0000000000000000000000000000e2e3"

# The mock Torznab indexer, and qBittorrent, both on the host network.
INDEXER_URL="http://127.0.0.1:9500"
INDEXER_API_KEY="e2e-indexer-key"
QBIT_HOST="127.0.0.1"
QBIT_PORT=8090

# OpenLibrary work ids, one per spec that needs its own book.
# Work ids, not edition ids: LazyLibrarian resolves no author from an edition id.
# Public domain titles, whose author and title parse cleanly into a release name.
WORK_IDS=(
  OL450063W   # Frankenstein, Mary Shelley
  OL450124W   # The Last Man, Mary Shelley
  OL85892W    # Dracula, Bram Stoker
  OL52267W    # The Time Machine, H. G. Wells
  OL52114W    # The War of the Worlds, H. G. Wells
  OL52266W    # The Invisible Man, H. G. Wells
  OL24034W    # Treasure Island, Robert Louis Stevenson
  OL24166W    # Kidnapped, Robert Louis Stevenson
  OL102749W   # Moby Dick, Herman Melville
  OL8721462W  # Great Expectations, Charles Dickens
  OL8193465W  # A Tale of Two Cities, Charles Dickens
  OL8193478W  # Oliver Twist, Charles Dickens
  OL1095427W  # Jane Eyre, Charlotte Bronte
  OL21177W    # Wuthering Heights, Emily Bronte
  OL66554W    # Pride and Prejudice, Jane Austen
  OL66513W    # Emma, Jane Austen
)

log() {
  echo "[seed-lazylibrarian] $*"
}

# LazyLibrarian saves its in-memory config when it shuts down.
# The file is therefore written while the container is stopped, never before a restart.
write_config_ini() {
  local dir="$1"

  rm -rf "$dir"
  mkdir -p "$dir"
  cat > "$dir/config.ini" <<EOF
[GENERAL]
imp_preflang = en, English, eng, en-US, en-GB
ebook_dir = /books
download_dir = /downloads
match_ratio = 20

[API]
api_enabled = 1
api_key = $API_KEY
book_api = OpenLibrary

[LOGGING]
logdir = /config/log
loglevel = 2

[TELEMETRY]
telemetry_enable = 0

[SEARCHSCAN]
search_bookinterval = 1440
search_maginterval = 1440
scan_interval = 1440
searchrss_interval = 1440
wishlist_interval = 1440
versioncheck_interval = 1440
authorupdate_interval = 1440
seriesupdate_interval = 1440
totals_interval = 1440
goodreads_interval = 1440
hardcover_interval = 1440

[TORRENT]
tor_downloader_qbittorrent = 1

[QBITTORRENT]
qbittorrent_host = $QBIT_HOST
qbittorrent_port = $QBIT_PORT
qbittorrent_user = admin
qbittorrent_pass = adminadmin
qbittorrent_label = lazylibrarian

[Torznab_1]
dispname = E2E Torznab
enabled = 1
host = $INDEXER_URL
api = $INDEXER_API_KEY
booksearch = book
bookcat = 7020
generalsearch = search
extended = 1
manual = True
dltypes = A,E,M
EOF
  chmod -R a+rwX "$dir"
}

api() {
  local cmd="$1"
  shift
  local query=""
  local param
  for param in "$@"; do
    query="$query&$param"
  done

  curl -fsS --max-time 180 "$URL/api?apikey=$API_KEY&cmd=$cmd$query"
}

wait_for_lazylibrarian() {
  log "waiting for lazylibrarian"
  for _ in $(seq 1 120); do
    if curl -fsS -o /dev/null "$URL/"; then
      log "lazylibrarian is up"
      return 0
    fi
    sleep 2
  done

  echo "[seed-lazylibrarian] lazylibrarian did not start" >&2
  return 1
}

# The API answers only once the config has been read, so this doubles as a config check.
wait_for_api() {
  log "waiting for the api"
  for _ in $(seq 1 60); do
    if api getVersion | grep -q '"Success": true'; then
      log "api is up"
      return 0
    fi
    sleep 2
  done

  echo "[seed-lazylibrarian] the api never answered, check api_enabled" >&2
  return 1
}

# A book lands with an empty status, which keeps it out of LazyLibrarian's own searches.
# Each spec marks its own book Wanted and then searches for it.
add_books() {
  local work_id
  for work_id in "${WORK_IDS[@]}"; do
    log "adding $work_id"
    if [[ "$(api addBook "id=$work_id" 'wait=1')" != "true" ]]; then
      echo "[seed-lazylibrarian] OpenLibrary returned no metadata for $work_id" >&2
      return 1
    fi
  done
}

verify_books() {
  local count
  count=$(sqlite3 "$TEST_DATA/lazylibrarian-config/lazylibrarian.db" 'SELECT count(*) FROM books')

  if [[ "$count" -ne "${#WORK_IDS[@]}" ]]; then
    echo "[seed-lazylibrarian] expected ${#WORK_IDS[@]} books, found $count" >&2
    return 1
  fi

  log "seeded $count books"
}

# Keeps config.ini and the database, drops the cache and the logs.
snapshot() {
  local src="$TEST_DATA/lazylibrarian-config"

  log "snapshotting lazylibrarian"
  rm -rf "$SEED"
  mkdir -p "$SEED"
  cp "$src/config.ini" "$SEED/config.ini"

  # LazyLibrarian leaves committed rows in the write-ahead log.
  # The log comes along and is folded into the database before it is dropped.
  cp "$src/lazylibrarian.db" "$SEED/lazylibrarian.db"
  for suffix in wal shm; do
    if [[ -f "$src/lazylibrarian.db-$suffix" ]]; then
      cp "$src/lazylibrarian.db-$suffix" "$SEED/lazylibrarian.db-$suffix"
    fi
  done

  sqlite3 "$SEED/lazylibrarian.db" 'PRAGMA journal_mode=DELETE; VACUUM;' > /dev/null
  rm -f "$SEED/lazylibrarian.db-wal" "$SEED/lazylibrarian.db-shm"
  chmod -R a+rwX "$SEED"
}

main() {
  bash "$HERE/scripts/setup-test-data.sh" > /dev/null

  write_config_ini "$TEST_DATA/lazylibrarian-config"

  log "starting lazylibrarian"
  $COMPOSE up -d --force-recreate lazylibrarian

  wait_for_lazylibrarian
  wait_for_api

  add_books

  log "stopping lazylibrarian"
  $COMPOSE stop lazylibrarian

  verify_books
  snapshot

  log "seed written to $SEED"
}

main "$@"
