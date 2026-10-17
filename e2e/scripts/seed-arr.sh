#!/usr/bin/env bash
#
# Rebuild e2e/arr-seed from scratch.
#
# Boots the arrs empty, configures them over their own API, adds one item each.
# Snapshots /config into e2e/arr-seed, which the e2e run restores.
# CI therefore needs no network and every run sees the same library.
#
# Needs internet: Sonarr reads Skyhook and Radarr reads TMDB.
# Needs ffmpeg: builds the two tiny media files the upgrade-detection spec imports.
# Stops the sonarr and radarr containers.
#
# Re-run it when the pinned image tags change, or when the library has to change.
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
# The Wild Robot, added second and left unmonitored.
# The specs that need two candidates monitor it themselves and restore it after.
RADARR_SECOND_TMDB_ID=1184918
# Weapons: a third movie seeded with a file, for #381's upgrade-detection specs.
# F1 and Wild Robot stay fileless for the specs that depend on them.
RADARR_UPGRADE_TMDB_ID=1078605

# S01E01's episode id equals its series id, and seeker-replacement-grab.api.spec.ts owns S01E02.
# #381's specs use S01E04 and S01E05.
SONARR_UPGRADE_EPISODE_ID=5

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

# An arr validates the posted item before it enriches it from its metadata provider.
# An add therefore carries the whole lookup result, not just the id.
merge_add_options() {
  local root_folder="$1" add_options="$2" monitored="${3:-true}"

  python3 -c '
import json
import sys

item = json.load(sys.stdin)[0]
item.update({
    "qualityProfileId": 1,
    "rootFolderPath": sys.argv[1],
    "monitored": sys.argv[3] == "true",
    "seasonFolder": True,
    "minimumAvailability": "released",
    "addOptions": json.loads(sys.argv[2]),
})
print(json.dumps(item))
' "$root_folder" "$add_options" "$monitored"
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

build_media_file() {
  local path="$1"

  ffmpeg -y -loglevel error -f lavfi -i color=c=black:s=320x240:d=1 -c:v libx264 -pix_fmt yuv420p "$path"
}

wait_for_command() {
  local url="$1" api_key="$2" command_id="$3" name="$4"

  log "waiting for $name"
  for _ in $(seq 1 60); do
    local status
    status=$(arr_get "$url" "$api_key" "/api/v3/command/$command_id" \
      | python3 -c 'import sys,json; print(json.load(sys.stdin)["status"])')

    if [[ "$status" == "completed" ]]; then
      return 0
    fi
    if [[ "$status" == "failed" ]]; then
      echo "[seed-arr] $name failed" >&2
      return 1
    fi
    sleep 1
  done

  echo "[seed-arr] $name timed out" >&2
  return 1
}

# Waits until the arr reports at least one item of the given kind.
# The add call returns before the metadata refresh finishes.
wait_for_items() {
  local url="$1" api_key="$2" path="$3" name="$4"

  log "waiting for $name"
  for _ in $(seq 1 90); do
    local count
    count=$(arr_get "$url" "$api_key" "$path" | python3 -c 'import sys,json; print(len(json.load(sys.stdin)))' 2>/dev/null || echo 0)
    if [[ "$count" -gt 0 ]]; then
      log "$name ready ($count)"
      return 0
    fi
    sleep 2
  done

  echo "[seed-arr] $name never appeared" >&2
  return 1
}

# forceSave skips the field validation, but not the connection test.
# The mock indexer has to be up and answering caps.
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

  # firstSeason leaves the specials unmonitored.
  # The Seeker then has one season to pick and the spec's release title is fixed.
  log "adding the series"
  local series
  series=$(arr_get "$SONARR_URL" "$SONARR_API_KEY" "/api/v3/series/lookup?term=tvdb%3A$SONARR_TVDB_ID" \
    | merge_add_options '/tv' '{"monitor":"firstSeason","searchForMissingEpisodes":false,"searchForCutoffUnmetEpisodes":false}')
  arr_post "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/series' "$series" > /dev/null

  wait_for_items "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/episode?seriesId=1' 'sonarr episodes'

  seed_sonarr_upgrade_file
}

# Gives S01E05 a file, for #381's upgrade-detection specs.
seed_sonarr_upgrade_file() {
  log "importing an episode file for the upgrade spec"

  local series_path
  series_path=$(arr_get "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/series/1' \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["path"])')

  local filename="Agatha.All.Along.S01E05.1080p.WEB-DL.DDP5.1.H.264-SEED.mkv"
  local host_dir="$TEST_DATA/sonarr-tv/$(basename "$series_path")/Season 01"
  mkdir -p "$host_dir"
  build_media_file "$host_dir/$filename"

  local command_id
  command_id=$(arr_post "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/command' "$(cat <<EOF
{
  "name": "ManualImport",
  "files": [{
    "path": "$series_path/Season 01/$filename",
    "seriesId": 1,
    "episodeIds": [$SONARR_UPGRADE_EPISODE_ID],
    "quality": { "quality": { "id": 3 }, "revision": { "version": 1, "real": 0, "isRepack": false } },
    "languages": [{ "id": 1, "name": "English" }],
    "indexerFlags": 0,
    "releaseType": "singleEpisode"
  }],
  "importMode": "move"
}
EOF
)" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

  wait_for_command "$SONARR_URL" "$SONARR_API_KEY" "$command_id" 'sonarr upgrade episode import'

  local has_file
  has_file=$(arr_get "$SONARR_URL" "$SONARR_API_KEY" "/api/v3/episode/$SONARR_UPGRADE_EPISODE_ID" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["hasFile"])')

  if [[ "$has_file" != "True" ]]; then
    echo "[seed-arr] sonarr upgrade episode never got a file" >&2
    return 1
  fi
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

  log "adding the second movie"
  local second
  second=$(arr_get "$RADARR_URL" "$RADARR_API_KEY" "/api/v3/movie/lookup?term=tmdb%3A$RADARR_SECOND_TMDB_ID" \
    | merge_add_options '/movies' '{"searchForMovie":false}' 'false')
  arr_post "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' "$second" > /dev/null

  log "adding the upgrade movie"
  local upgrade
  upgrade=$(arr_get "$RADARR_URL" "$RADARR_API_KEY" "/api/v3/movie/lookup?term=tmdb%3A$RADARR_UPGRADE_TMDB_ID" \
    | merge_add_options '/movies' '{"searchForMovie":false}')
  arr_post "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' "$upgrade" > /dev/null

  wait_for_items "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' 'radarr movies'

  seed_radarr_upgrade_file
}

# Gives the third movie a file, for #381's upgrade-detection specs.
seed_radarr_upgrade_file() {
  log "importing a movie file for the upgrade spec"

  local movie
  movie=$(arr_get "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' | python3 -c "
import json, sys
movie = next(m for m in json.load(sys.stdin) if m['tmdbId'] == $RADARR_UPGRADE_TMDB_ID)
print(json.dumps({'id': movie['id'], 'path': movie['path']}))
")
  local movie_id movie_path
  movie_id=$(echo "$movie" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
  movie_path=$(echo "$movie" | python3 -c 'import json,sys; print(json.load(sys.stdin)["path"])')

  local filename="Weapons.2025.1080p.WEB-DL.DDP5.1.H.264-SEED.mkv"
  local host_dir="$TEST_DATA/radarr-movies/$(basename "$movie_path")"
  mkdir -p "$host_dir"
  build_media_file "$host_dir/$filename"

  local command_id
  command_id=$(arr_post "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/command' "$(cat <<EOF
{
  "name": "ManualImport",
  "files": [{
    "path": "$movie_path/$filename",
    "movieId": $movie_id,
    "quality": { "quality": { "id": 8 }, "revision": { "version": 1, "real": 0, "isRepack": false } },
    "languages": [{ "id": 1, "name": "English" }],
    "indexerFlags": 0
  }],
  "importMode": "move"
}
EOF
)" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')

  wait_for_command "$RADARR_URL" "$RADARR_API_KEY" "$command_id" 'radarr upgrade movie import'

  local has_file
  has_file=$(arr_get "$RADARR_URL" "$RADARR_API_KEY" "/api/v3/movie/$movie_id" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin)["hasFile"])')

  if [[ "$has_file" != "True" ]]; then
    echo "[seed-arr] radarr upgrade movie never got a file" >&2
    return 1
  fi
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

  # The arr leaves committed rows in the write-ahead log.
  # The log comes along and is folded into the database before it is dropped.
  cp "$src/$arr.db" "$dest/$arr.db"
  for suffix in wal shm; do
    if [[ -f "$src/$arr.db-$suffix" ]]; then
      cp "$src/$arr.db-$suffix" "$dest/$arr.db-$suffix"
    fi
  done

  sqlite3 "$dest/$arr.db" 'PRAGMA journal_mode=DELETE; VACUUM;' > /dev/null
  rm -f "$dest/$arr.db-wal" "$dest/$arr.db-shm"

  # Carries the on-disk file the upgrade spec's moviefile/episodefile row points at.
  # setup-test-data.sh restores it before every run, or the arr unlinks the row on its next disk scan.
  case "$arr" in
    radarr) cp -R "$TEST_DATA/radarr-movies" "$dest/movies" ;;
    sonarr) cp -R "$TEST_DATA/sonarr-tv" "$dest/tv" ;;
  esac
}

main() {
  bash "$HERE/scripts/setup-test-data.sh" > /dev/null

  write_config_xml "$TEST_DATA/sonarr-config" 8989 "$SONARR_API_KEY" 'Sonarr'
  write_config_xml "$TEST_DATA/radarr-config" 7878 "$RADARR_API_KEY" 'Radarr'

  # An arr tests the indexer and the download client before it saves them.
  # Both have to answer during seeding.
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
