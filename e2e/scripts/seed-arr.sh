#!/usr/bin/env bash
#
# Rebuild e2e/arr-seed from scratch.
#
# Boots Sonarr and Radarr on an empty config, configures them through their own
# API, adds one library item each, then snapshots /config into e2e/arr-seed.
# The e2e run restores that snapshot, so CI needs no network and every run sees
# the same library.
#
# This script needs internet: Sonarr reads Skyhook and Radarr reads TMDB to
# resolve the library item. It also stops the sonarr and radarr containers.
#
# Re-run it when the pinned image tags in docker-compose.e2e.yml change, or when
# the seeded library has to change.
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DATA="$HERE/test-data"
ARR_SEED="$HERE/arr-seed"
COMPOSE="docker compose -f $HERE/docker-compose.e2e.yml"

SONARR_URL="http://localhost:8989"
RADARR_URL="http://localhost:7878"
SONARR_API_KEY="0000000000000000000000000000e2e1"
RADARR_API_KEY="0000000000000000000000000000e2e2"

# The mock Torznab indexer, and qBittorrent, both on the host network.
INDEXER_URL="http://127.0.0.1:9500"
INDEXER_API_KEY="e2e-indexer-key"
QBIT_HOST="127.0.0.1"
QBIT_PORT=8090

# Agatha All Along: one season, recent, unambiguous release names.
SONARR_TVDB_ID=412429
# F1: recent, and its releases parse cleanly against the movie title plus year.
RADARR_TMDB_ID=911430

log() {
  echo "[seed-arr] $*"
}

write_config_xml() {
  local dir="$1" port="$2" api_key="$3" instance="$4"

  rm -rf "$dir"
  mkdir -p "$dir"
  cat > "$dir/config.xml" <<EOF
<Config>
  <BindAddress>*</BindAddress>
  <Port>$port</Port>
  <EnableSsl>False</EnableSsl>
  <LaunchBrowser>False</LaunchBrowser>
  <ApiKey>$api_key</ApiKey>
  <AuthenticationMethod>External</AuthenticationMethod>
  <AuthenticationRequired>DisabledForLocalAddresses</AuthenticationRequired>
  <Branch>main</Branch>
  <LogLevel>info</LogLevel>
  <UrlBase></UrlBase>
  <InstanceName>$instance</InstanceName>
  <UpdateMechanism>Docker</UpdateMechanism>
  <AnalyticsEnabled>False</AnalyticsEnabled>
</Config>
EOF
  chmod -R a+rwX "$dir"
}

arr_post() {
  local url="$1" api_key="$2" path="$3" body="$4"

  curl -fsS -X POST "$url$path" \
    -H "X-Api-Key: $api_key" \
    -H 'Content-Type: application/json' \
    -d "$body"
}

arr_get() {
  local url="$1" api_key="$2" path="$3"

  curl -fsS "$url$path" -H "X-Api-Key: $api_key"
}

# The arrs validate the posted item before they enrich it from their metadata
# provider, so an add has to carry the whole lookup result, not just the id.
merge_add_options() {
  local root_folder="$1" add_options="$2"

  python3 -c '
import json
import sys

item = json.load(sys.stdin)[0]
item.update({
    "qualityProfileId": 1,
    "rootFolderPath": sys.argv[1],
    "monitored": True,
    "seasonFolder": True,
    "minimumAvailability": "released",
    "addOptions": json.loads(sys.argv[2]),
})
print(json.dumps(item))
' "$root_folder" "$add_options"
}

wait_for_arr() {
  local url="$1" name="$2"

  log "waiting for $name"
  for _ in $(seq 1 120); do
    if curl -fsS -o /dev/null "$url/ping"; then
      log "$name is up"
      return 0
    fi
    sleep 2
  done

  echo "[seed-arr] $name did not start" >&2
  return 1
}

# Waits until the arr reports at least one item of the given kind. The add call
# returns before the metadata refresh finishes.
wait_for_items() {
  local url="$1" api_key="$2" path="$3" name="$4"

  log "waiting for $name"
  for _ in $(seq 1 90); do
    local count
    count=$(arr_get "$url" "$api_key" "$path" | python3 -c 'import sys,json; print(len(json.load(sys.stdin)))' 2>/dev/null || echo 0)
    if [ "$count" -gt 0 ]; then
      log "$name ready ($count)"
      return 0
    fi
    sleep 2
  done

  echo "[seed-arr] $name never appeared" >&2
  return 1
}

# forceSave skips the connection test, so seeding needs neither the mock indexer
# nor qBittorrent to be running.
add_indexer() {
  local url="$1" api_key="$2" categories="$3"

  arr_post "$url" "$api_key" '/api/v3/indexer?forceSave=true' "$(cat <<EOF
{
  "name": "E2E Torznab",
  "implementation": "Torznab",
  "implementationName": "Torznab",
  "configContract": "TorznabSettings",
  "protocol": "torrent",
  "enableRss": false,
  "enableAutomaticSearch": true,
  "enableInteractiveSearch": true,
  "supportsRss": true,
  "supportsSearch": true,
  "priority": 25,
  "tags": [],
  "fields": [
    { "name": "baseUrl", "value": "$INDEXER_URL" },
    { "name": "apiPath", "value": "/api" },
    { "name": "apiKey", "value": "$INDEXER_API_KEY" },
    { "name": "categories", "value": $categories },
    { "name": "animeCategories", "value": [] },
    { "name": "minimumSeeders", "value": 1 }
  ]
}
EOF
)" > /dev/null
}

add_download_client() {
  local url="$1" api_key="$2" category_field="$3" category="$4"

  arr_post "$url" "$api_key" '/api/v3/downloadclient?forceSave=true' "$(cat <<EOF
{
  "name": "qBittorrent",
  "implementation": "QBittorrent",
  "implementationName": "qBittorrent",
  "configContract": "QBittorrentSettings",
  "protocol": "torrent",
  "enable": true,
  "priority": 1,
  "removeCompletedDownloads": false,
  "removeFailedDownloads": false,
  "tags": [],
  "fields": [
    { "name": "host", "value": "$QBIT_HOST" },
    { "name": "port", "value": $QBIT_PORT },
    { "name": "useSsl", "value": false },
    { "name": "urlBase", "value": "" },
    { "name": "username", "value": "admin" },
    { "name": "password", "value": "adminadmin" },
    { "name": "$category_field", "value": "$category" },
    { "name": "initialState", "value": 0 },
    { "name": "sequentialOrder", "value": false },
    { "name": "firstAndLast", "value": false }
  ]
}
EOF
)" > /dev/null
}

seed_sonarr() {
  log "configuring sonarr"
  arr_post "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/rootfolder' '{"path":"/tv"}' > /dev/null
  add_indexer "$SONARR_URL" "$SONARR_API_KEY" '[5000]'
  add_download_client "$SONARR_URL" "$SONARR_API_KEY" 'tvCategory' 'tv-sonarr'

  # firstSeason leaves the specials unmonitored, so the Seeker has exactly one
  # season to pick and the release title the spec stubs is deterministic.
  log "adding the series"
  local series
  series=$(arr_get "$SONARR_URL" "$SONARR_API_KEY" "/api/v3/series/lookup?term=tvdb%3A$SONARR_TVDB_ID" \
    | merge_add_options '/tv' '{"monitor":"firstSeason","searchForMissingEpisodes":false,"searchForCutoffUnmetEpisodes":false}')
  arr_post "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/series' "$series" > /dev/null

  wait_for_items "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/episode?seriesId=1' 'sonarr episodes'
}

seed_radarr() {
  log "configuring radarr"
  arr_post "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/rootfolder' '{"path":"/movies"}' > /dev/null
  add_indexer "$RADARR_URL" "$RADARR_API_KEY" '[2000]'
  add_download_client "$RADARR_URL" "$RADARR_API_KEY" 'movieCategory' 'radarr'

  log "adding the movie"
  local movie
  movie=$(arr_get "$RADARR_URL" "$RADARR_API_KEY" "/api/v3/movie/lookup?term=tmdb%3A$RADARR_TMDB_ID" \
    | merge_add_options '/movies' '{"searchForMovie":false}')
  arr_post "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' "$movie" > /dev/null

  wait_for_items "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' 'radarr movies'
}

# Keeps the database and config.xml, drops everything the arr rebuilds on start.
snapshot() {
  local arr="$1"
  local src="$TEST_DATA/$arr-config"
  local dest="$ARR_SEED/$arr"

  log "snapshotting $arr"
  rm -rf "$dest"
  mkdir -p "$dest"
  cp "$src/config.xml" "$dest/config.xml"

  # The arr leaves committed rows in the write-ahead log, so the log comes along
  # and is folded into the database before it is dropped.
  cp "$src/$arr.db" "$dest/$arr.db"
  for suffix in wal shm; do
    if [ -f "$src/$arr.db-$suffix" ]; then
      cp "$src/$arr.db-$suffix" "$dest/$arr.db-$suffix"
    fi
  done

  sqlite3 "$dest/$arr.db" 'PRAGMA journal_mode=DELETE; VACUUM;' > /dev/null
  rm -f "$dest/$arr.db-wal" "$dest/$arr.db-shm"
}

main() {
  bash "$HERE/scripts/setup-test-data.sh" > /dev/null

  write_config_xml "$TEST_DATA/sonarr-config" 8989 "$SONARR_API_KEY" 'Sonarr'
  write_config_xml "$TEST_DATA/radarr-config" 7878 "$RADARR_API_KEY" 'Radarr'

  # The arrs test the indexer and the download client before saving them, so both
  # have to answer during seeding.
  log "starting the arrs"
  $COMPOSE up -d --force-recreate wiremock-indexer qbittorrent sonarr radarr

  wait_for_arr "$SONARR_URL" 'sonarr'
  wait_for_arr "$RADARR_URL" 'radarr'

  seed_sonarr
  seed_radarr

  log "stopping the arrs"
  $COMPOSE stop wiremock-indexer qbittorrent sonarr radarr

  snapshot sonarr
  snapshot radarr

  log "seed written to $ARR_SEED"
}

main "$@"
