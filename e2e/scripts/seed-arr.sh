#!/usr/bin/env bash
#
# Rebuild e2e/arr-seed from scratch.
#
# Boots the arrs empty, configures them over their own API, adds one item each.
# Snapshots /config into e2e/arr-seed, which the e2e run restores.
# CI therefore needs no network and every run sees the same library.
#
# Needs internet: each arr reads its metadata provider.
# Stops the sonarr, radarr, whisparr, whisparr2, lidarr, chaptarr and sportarr containers.
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
WHISPARR_URL="http://localhost:6970"
WHISPARR2_URL="http://localhost:6971"
LIDARR_URL="http://localhost:8686"
CHAPTARR_URL="http://localhost:8789"
SPORTARR_URL="http://localhost:1867"
SONARR_API_KEY="0000000000000000000000000000e2e1"
RADARR_API_KEY="0000000000000000000000000000e2e2"
WHISPARR_API_KEY="0000000000000000000000000000e2e6"
LIDARR_API_KEY="0000000000000000000000000000e2e7"
WHISPARR2_API_KEY="0000000000000000000000000000e2e8"
CHAPTARR_API_KEY="0000000000000000000000000000e2e9"
SPORTARR_API_KEY="0000000000000000000000000000e2ea"
ROOTFOLDER_V3="/api/v3/rootfolder"

# The mock Torznab indexer, and qBittorrent, both on the host network.
INDEXER_URL="http://127.0.0.1:9500"
INDEXER_API_KEY="e2e-indexer-key"
QBIT_HOST="127.0.0.1"
QBIT_PORT=8090

# Agatha All Along: one season, recent, unambiguous release names.
SONARR_TVDB_ID=412429
# Sherlock: aired specials, added unmonitored.
# The specials spec monitors one special itself and restores it after.
SONARR_SPECIALS_TVDB_ID=176941
# F1: recent, and its releases parse cleanly against the movie title plus year.
RADARR_TMDB_ID=911430
# The Wild Robot, added second and left unmonitored.
# The specs that need two candidates monitor it themselves and restore it after.
RADARR_SECOND_TMDB_ID=1184918
# Package Holiday (2004): a plain title, since the seed database is public.
WHISPARR_TMDB_ID=1253309
# Tushy: a studio with years of scenes, pruned to a fixed handful after the add.
WHISPARR2_TVDB_ID=3417
# Jeff Buckley: one studio album and a plain name.
LIDARR_MBID=e6e879c0-3d56-4f12-b3c5-3ce459661a8e
# Delia Ray, Hardcover author hc:142722: eight titles, and Finding Fortune is the one left monitored.
# The lookup term is not the author id, hc:5000 resolves to her.
CHAPTARR_AUTHOR_TERM='hc:5000'
CHAPTARR_WANTED_TITLE='Finding Fortune'
# UFC and Bellator, each limited to the current season at the add and pruned to a fixed few events after.
SPORTARR_UFC_ID='lg-000079'
SPORTARR_BELLATOR_ID='lg-000103'

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

# Sportarr reads config.xml with an XML serializer, which rejects the capitalised booleans above.
# One bad value drops the whole file and the API key with it.
write_sportarr_config() {
  local dir="$TEST_DATA/sportarr-config"

  rm -rf "$dir"
  mkdir -p "$dir"
  cat > "$dir/config.xml" <<EOF
<Config>
  <ApiKey>$SPORTARR_API_KEY</ApiKey>
  <AuthenticationMethod>None</AuthenticationMethod>
  <AuthenticationRequired>DisabledForLocalAddresses</AuthenticationRequired>
  <Port>1867</Port>
  <InstanceName>Sportarr</InstanceName>
  <LogLevel>info</LogLevel>
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

# Waits until the arr reports at least one item of the given kind.
# The add call returns before the metadata refresh finishes.
wait_for_items() {
  local url="$1" api_key="$2" path="$3" name="$4"

  log "waiting for $name"
  for _ in $(seq 1 90); do
    local count
    count=$(arr_get "$url" "$api_key" "$path" | python3 -c 'import sys,json; d = json.load(sys.stdin); print(len(d["records"]) if isinstance(d, dict) else len(d))' 2>/dev/null || echo 0)
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
  local url="$1" api_key="$2" categories="$3" api_version="${4:-v3}"

  arr_post "$url" "$api_key" "/api/$api_version/indexer?forceSave=true" "$(cat <<EOF
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

# Whisparr v2 rejects qBittorrent's 204 login reply, so it is seeded without credentials.
# qBittorrent trusts the host network, which makes the login unnecessary.
add_download_client() {
  local url="$1" api_key="$2" category_field="$3" category="$4" api_version="${5:-v3}"
  local username="${6-admin}" password="${7-adminadmin}" second_category_field="${8:-}"
  local second_category=""
  if [[ -n "$second_category_field" ]]; then
    second_category="{ \"name\": \"$second_category_field\", \"value\": \"$category\" },"
  fi

  arr_post "$url" "$api_key" "/api/$api_version/downloadclient?forceSave=true" "$(cat <<EOF
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
    { "name": "username", "value": "$username" },
    { "name": "password", "value": "$password" },
    { "name": "$category_field", "value": "$category" },
    $second_category
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
  arr_post "$SONARR_URL" "$SONARR_API_KEY" "$ROOTFOLDER_V3" '{"path":"/tv"}' > /dev/null
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

  log "adding the specials series"
  local specials
  specials=$(arr_get "$SONARR_URL" "$SONARR_API_KEY" "/api/v3/series/lookup?term=tvdb%3A$SONARR_SPECIALS_TVDB_ID" \
    | merge_add_options '/tv' '{"monitor":"none","searchForMissingEpisodes":false,"searchForCutoffUnmetEpisodes":false}' 'false')
  arr_post "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/series' "$specials" > /dev/null

  wait_for_items "$SONARR_URL" "$SONARR_API_KEY" '/api/v3/episode?seriesId=2' 'sonarr specials episodes'
}

seed_radarr() {
  log "configuring radarr"
  arr_post "$RADARR_URL" "$RADARR_API_KEY" "$ROOTFOLDER_V3" '{"path":"/movies"}' > /dev/null
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

  wait_for_items "$RADARR_URL" "$RADARR_API_KEY" '/api/v3/movie' 'radarr movies'
}

seed_whisparr() {
  log "configuring whisparr"
  arr_post "$WHISPARR_URL" "$WHISPARR_API_KEY" "$ROOTFOLDER_V3" '{"path":"/movies"}' > /dev/null
  add_indexer "$WHISPARR_URL" "$WHISPARR_API_KEY" '[6000]'
  add_download_client "$WHISPARR_URL" "$WHISPARR_API_KEY" 'movieCategory' 'whisparr'

  log "adding the whisparr movie"
  local movie
  movie=$(arr_get "$WHISPARR_URL" "$WHISPARR_API_KEY" "/api/v3/movie/lookup?term=tmdb%3A$WHISPARR_TMDB_ID" \
    | merge_add_options '/movies' '{"searchForMovie":false}')
  arr_post "$WHISPARR_URL" "$WHISPARR_API_KEY" '/api/v3/movie' "$movie" > /dev/null

  wait_for_items "$WHISPARR_URL" "$WHISPARR_API_KEY" '/api/v3/movie' 'whisparr movies'
}

seed_whisparr2() {
  log "configuring whisparr2"
  arr_post "$WHISPARR2_URL" "$WHISPARR2_API_KEY" "$ROOTFOLDER_V3" '{"path":"/scenes"}' > /dev/null
  add_indexer "$WHISPARR2_URL" "$WHISPARR2_API_KEY" '[6000]'
  add_download_client "$WHISPARR2_URL" "$WHISPARR2_API_KEY" 'tvCategory' 'whisparr2' 'v3' '' ''

  # The lookup takes a site name, not an id, so the result is picked by tvdbId.
  log "adding the whisparr2 site"
  local site
  site=$(arr_get "$WHISPARR2_URL" "$WHISPARR2_API_KEY" '/api/v3/series/lookup?term=tushy' \
    | python3 -c '
import json
import sys

item = next(x for x in json.load(sys.stdin) if x["tvdbId"] == int(sys.argv[1]))
item.update({
    "qualityProfileId": 1,
    "rootFolderPath": "/scenes",
    "monitored": True,
    "seasonFolder": True,
    "addOptions": {"monitor": "all", "searchForMissingEpisodes": False, "searchForCutoffUnmetEpisodes": False},
})
print(json.dumps(item))
' "$WHISPARR2_TVDB_ID")
  arr_post "$WHISPARR2_URL" "$WHISPARR2_API_KEY" '/api/v3/series' "$site" > /dev/null

  wait_for_items "$WHISPARR2_URL" "$WHISPARR2_API_KEY" '/api/v3/episode?seriesId=1' 'whisparr2 scenes'
  # The add returns before the scene list is complete.
  sleep 30
}

seed_lidarr() {
  log "configuring lidarr"
  arr_post "$LIDARR_URL" "$LIDARR_API_KEY" '/api/v1/rootfolder' \
    '{"name":"Music","path":"/music","defaultMetadataProfileId":1,"defaultQualityProfileId":1,"defaultMonitorOption":"all"}' > /dev/null
  add_indexer "$LIDARR_URL" "$LIDARR_API_KEY" '[3000]' 'v1'
  add_download_client "$LIDARR_URL" "$LIDARR_API_KEY" 'musicCategory' 'lidarr' 'v1'

  log "adding the artist"
  local artist
  artist=$(arr_get "$LIDARR_URL" "$LIDARR_API_KEY" "/api/v1/artist/lookup?term=lidarr%3A$LIDARR_MBID" \
    | python3 -c '
import json
import sys

item = json.load(sys.stdin)[0]
item.update({
    "qualityProfileId": 1,
    "metadataProfileId": 1,
    "rootFolderPath": "/music",
    "monitored": True,
    "addOptions": {"monitor": "all", "searchForMissingAlbums": False},
})
print(json.dumps(item))
')
  arr_post "$LIDARR_URL" "$LIDARR_API_KEY" '/api/v1/artist' "$artist" > /dev/null

  wait_for_items "$LIDARR_URL" "$LIDARR_API_KEY" '/api/v1/wanted/missing' 'lidarr missing albums'
}

seed_chaptarr() {
  log "configuring chaptarr"
  # Profile 1 is E-Book and 2 is Audiobook, metadata profile 1 is Audiobook Default and 2 is Ebook Default.
  arr_post "$CHAPTARR_URL" "$CHAPTARR_API_KEY" '/api/v1/rootfolder' \
    '{"name":"Books","path":"/books","folderType":0,"defaultMonitorOption":"all","audiobookQualityProfileId":2,"audiobookMetadataProfileId":1,"ebookQualityProfileId":1,"ebookMetadataProfileId":2}' > /dev/null
  add_indexer "$CHAPTARR_URL" "$CHAPTARR_API_KEY" '[3030,7020]' 'v1'
  add_download_client "$CHAPTARR_URL" "$CHAPTARR_API_KEY" 'audiobookCategory' 'chaptarr' 'v1' admin adminadmin 'ebookCategory'

  log "adding the author"
  local author
  author=$(arr_get "$CHAPTARR_URL" "$CHAPTARR_API_KEY" "/api/v1/author/lookup?term=${CHAPTARR_AUTHOR_TERM/:/%3A}" \
    | python3 -c '
import json
import sys

item = json.load(sys.stdin)[0]
item.update({
    "monitored": True,
    "audiobookMonitored": True,
    "ebookMonitored": True,
    "rootFolderPath": "/books",
    "path": "/books/" + item["authorName"],
    "audiobookQualityProfileId": 2,
    "audiobookMetadataProfileId": 1,
    "ebookQualityProfileId": 1,
    "ebookMetadataProfileId": 2,
    "addOptions": {"monitor": "all", "searchForMissingBooks": False},
})
print(json.dumps(item))
')
  arr_post "$CHAPTARR_URL" "$CHAPTARR_API_KEY" '/api/v1/author' "$author" > /dev/null

  wait_for_items "$CHAPTARR_URL" "$CHAPTARR_API_KEY" '/api/v1/wanted/missing' 'chaptarr missing books'
  # The add returns before the bibliography is complete.
  sleep 20

  # Each title has an audiobook row and an ebook row.
  # Only the wanted title keeps its two rows monitored.
  log "unmonitoring all but one title"
  local others
  others=$(arr_get "$CHAPTARR_URL" "$CHAPTARR_API_KEY" '/api/v1/book?authorId=1' \
    | python3 -c '
import json
import sys

print(json.dumps({"bookIds": [b["id"] for b in json.load(sys.stdin) if b["title"] != sys.argv[1]], "monitored": False}))
' "$CHAPTARR_WANTED_TITLE")
  curl -fsS -X PUT "$CHAPTARR_URL/api/v1/book/monitor" \
    -H "X-Api-Key: $CHAPTARR_API_KEY" -H 'Content-Type: application/json' -d "$others" > /dev/null
}

seed_sportarr() {
  log "configuring sportarr"
  arr_post "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/rootfolder' '{"path":"/events"}' > /dev/null
  # Its v3 compatibility routes cannot create an indexer or a download client.
  arr_post "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/indexer' "$(cat <<EOF
{
  "name": "E2E Torznab",
  "implementation": "Torznab",
  "enable": true,
  "enableRss": false,
  "enableAutomaticSearch": true,
  "enableInteractiveSearch": true,
  "priority": 25,
  "fields": [
    { "name": "baseUrl", "value": "$INDEXER_URL" },
    { "name": "apiPath", "value": "/api" },
    { "name": "apiKey", "value": "$INDEXER_API_KEY" },
    { "name": "categories", "value": [5000] },
    { "name": "minimumSeeders", "value": 1 }
  ]
}
EOF
)" > /dev/null
  arr_post "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/downloadclient' "$(cat <<EOF
{
  "name": "qBittorrent",
  "type": 0,
  "host": "$QBIT_HOST",
  "port": $QBIT_PORT,
  "username": "admin",
  "password": "adminadmin",
  "category": "sportarr",
  "enabled": true,
  "priority": 1,
  "removeCompletedDownloads": false,
  "removeFailedDownloads": false
}
EOF
)" > /dev/null

  # The events come from sportarr.net, which a deep sync walks one season at a time.
  log "adding the leagues"
  arr_post "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/leagues' \
    "{\"externalId\":\"$SPORTARR_UFC_ID\",\"name\":\"UFC\",\"sport\":\"Combat\",\"sportFormat\":\"EventSport\",\"monitorType\":2,\"qualityProfileId\":1,\"rootFolderId\":1}" > /dev/null
  arr_post "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/leagues' \
    "{\"externalId\":\"$SPORTARR_BELLATOR_ID\",\"name\":\"Bellator\",\"sport\":\"Combat\",\"sportFormat\":\"EventSport\",\"monitorType\":2,\"qualityProfileId\":1,\"rootFolderId\":1}" > /dev/null

  log "waiting for the league sync"
  for _ in $(seq 1 180); do
    local running
    running=$(arr_get "$SPORTARR_URL" "$SPORTARR_API_KEY" '/api/task' | python3 -c 'import sys,json; print(sum(1 for t in json.load(sys.stdin) if t["status"] in ("Queued", "Running")))')
    if [[ "$running" -eq 0 ]]; then
      return 0
    fi
    sleep 5
  done

  echo "[seed-arr] sportarr league sync never finished" >&2
  return 1
}

# Keeps the database and config.xml, drops everything the arr rebuilds on start.
# Whisparr v3 names its database whisparr3.db, v2 whisparr2.db.
snapshot() {
  local arr="$1" db="${2:-$1}"
  local src="$TEST_DATA/$arr-config"
  local dest="$ARR_SEED/$arr"

  log "snapshotting $arr"
  rm -rf "$dest"
  mkdir -p "$dest"
  cp "$src/config.xml" "$dest/config.xml"

  # The arr leaves committed rows in the write-ahead log.
  # The log comes along and is folded into the database before it is dropped.
  cp "$src/$db.db" "$dest/$db.db"
  for suffix in wal shm; do
    if [[ -f "$src/$db.db-$suffix" ]]; then
      cp "$src/$db.db-$suffix" "$dest/$db.db-$suffix"
    fi
  done

  sqlite3 "$dest/$db.db" 'PRAGMA journal_mode=DELETE; VACUUM;' > /dev/null
  rm -f "$dest/$db.db-wal" "$dest/$db.db-shm"
}

main() {
  bash "$HERE/scripts/setup-test-data.sh" > /dev/null

  write_config_xml "$TEST_DATA/sonarr-config" 8989 "$SONARR_API_KEY" 'Sonarr'
  write_config_xml "$TEST_DATA/radarr-config" 7878 "$RADARR_API_KEY" 'Radarr'
  write_config_xml "$TEST_DATA/whisparr-config" 6970 "$WHISPARR_API_KEY" 'Whisparr'
  write_config_xml "$TEST_DATA/whisparr2-config" 6971 "$WHISPARR2_API_KEY" 'Whisparr2'
  write_config_xml "$TEST_DATA/lidarr-config" 8686 "$LIDARR_API_KEY" 'Lidarr'
  write_config_xml "$TEST_DATA/chaptarr-config" 8789 "$CHAPTARR_API_KEY" 'Chaptarr'
  write_sportarr_config

  # An arr tests the indexer and the download client before it saves them.
  # Both have to answer during seeding.
  log "starting the arrs"
  $COMPOSE up -d --force-recreate wiremock-indexer qbittorrent sonarr radarr whisparr whisparr2 lidarr chaptarr sportarr

  wait_for_arr "$SONARR_URL" 'sonarr'
  wait_for_arr "$RADARR_URL" 'radarr'
  wait_for_arr "$WHISPARR_URL" 'whisparr'
  wait_for_arr "$WHISPARR2_URL" 'whisparr2'
  wait_for_arr "$LIDARR_URL" 'lidarr'
  wait_for_arr "$CHAPTARR_URL" 'chaptarr'
  wait_for_arr "$SPORTARR_URL" 'sportarr'

  seed_sonarr
  seed_radarr
  seed_whisparr
  seed_whisparr2
  seed_lidarr
  seed_chaptarr
  seed_sportarr

  log "stopping the arrs"
  $COMPOSE stop wiremock-indexer qbittorrent sonarr radarr whisparr whisparr2 lidarr chaptarr sportarr

  snapshot sonarr
  snapshot radarr
  snapshot whisparr whisparr3
  snapshot whisparr2
  snapshot lidarr
  snapshot chaptarr
  snapshot sportarr

  # The seed is public, so it keeps the title and drops the synopsis, posters and cast.
  sqlite3 "$ARR_SEED/whisparr/whisparr3.db" "UPDATE MovieMetadata SET Overview = '', Images = '[]', Credits = '[]'; VACUUM;"

  # Years 2019 and 2018 are the only ones kept.
  # 2019 has four monitored missing scenes, 2018 has three scenes with only the first monitored.
  # The second 2018 scene gets a file, so Auto searches that year by scene.
  # Titles, synopses and cast are dropped, since the seed is public.
  # The refresh timestamps sit in the future, or an online run refetches every scene.
  sqlite3 "$ARR_SEED/whisparr2/whisparr2.db" "
    DELETE FROM Episodes WHERE Id NOT IN (SELECT Id FROM Episodes WHERE SeasonNumber = 2019 ORDER BY AirDate LIMIT 4)
      AND Id NOT IN (SELECT Id FROM Episodes WHERE SeasonNumber = 2018 ORDER BY AirDate LIMIT 3);
    UPDATE Episodes SET Monitored = 1 WHERE SeasonNumber = 2019;
    UPDATE Episodes SET Monitored = (Id = (SELECT MIN(Id) FROM Episodes WHERE SeasonNumber = 2018)) WHERE SeasonNumber = 2018;
    UPDATE Episodes SET Title = 'Scene ' || Id, Overview = '', Images = '[]', Actors = '[]';
    INSERT INTO EpisodeFiles (SeriesId, Quality, Size, DateAdded, SeasonNumber, RelativePath, Languages)
      SELECT SeriesId, '{\"quality\":3,\"revision\":{\"version\":1,\"real\":0,\"isRepack\":false}}', 1, '2018-01-07 00:00:00Z', 2018, 'Scene ' || Id || '.mkv', '[1]'
      FROM Episodes WHERE SeasonNumber = 2018 ORDER BY Id LIMIT 1 OFFSET 1;
    UPDATE Episodes SET EpisodeFileId = (SELECT MAX(Id) FROM EpisodeFiles)
      WHERE Id = (SELECT Id FROM Episodes WHERE SeasonNumber = 2018 ORDER BY Id LIMIT 1 OFFSET 1);
    UPDATE Series SET Overview = '', Images = '[]',
      Seasons = (SELECT json_group_array(json(value)) FROM json_each(Seasons) WHERE json_extract(value, '\$.seasonNumber') IN (2018, 2019));
    UPDATE Series SET LastInfoSync = '2099-01-01 00:00:00Z';
    UPDATE ScheduledTasks SET LastExecution = '2099-01-01 00:00:00Z';
    VACUUM;"

  sqlite3 "$ARR_SEED/lidarr/lidarr.db" "UPDATE ArtistMetadata SET Overview = '', Images = '[]', Links = '[]'; UPDATE Albums SET Overview = '', Images = '[]', Links = '[]'; VACUUM;"

  # The refresh timestamps sit in the future, or an online run refetches the bibliography.
  sqlite3 "$ARR_SEED/chaptarr/chaptarr.db" "
    UPDATE Authors SET Overview = '', Images = '[]', Links = '[]', LastInfoSync = '2099-01-01 00:00:00Z';
    UPDATE Books SET Overview = '', Images = '[]', Links = '[]', LastInfoSync = '2099-01-01 00:00:00Z';
    UPDATE Editions SET Overview = '', Images = '[]', Links = '[]';
    UPDATE ScheduledTasks SET LastExecution = '2099-01-01 00:00:00Z';
    VACUUM;"

  # Four UFC events stay, one of them postponed, and two Bellator events in a league left unmonitored.
  # Ids run 1 to 7 in a fixed order, so a re-seed keeps them.
  # Fight Night 231 gets a 720p file below the profile cutoff, and the other monitored events stay missing.
  # Quality sizes drop to zero, or the 2 GiB release the specs advertise fails the minimum.
  # The search cache is off, or a second search in a spec replays the first one's releases.
  # The sync timestamps sit in the future, or an online run refetches every season.
  # The metadata hub points at a closed port, or its changes poller re-syncs every season after five minutes.
  sqlite3 "$ARR_SEED/sportarr/sportarr.db" "
    DELETE FROM Tasks;
    DELETE FROM IndexerStatuses;
    DELETE FROM SeasonPosters;
    DELETE FROM sqlite_sequence WHERE name IN ('Tasks', 'IndexerStatuses', 'SeasonPosters');
    DELETE FROM Events WHERE Title NOT IN (
      'UFC 296 Edwards vs Covington', 'UFC Fight Night 231 Almeida vs Lewis', 'UFC on ESPN 52 Dariush vs Tsarukyan',
      'UFC Fight Night 232 Allen vs Craig', 'UFC 151 Jones vs Henderson', 'Bellator 301', 'Bellator 300 Nurmagomedov vs Primus');
    UPDATE Events SET Id = Id + 100000;
    UPDATE Events SET Id = CASE Title
      WHEN 'UFC 296 Edwards vs Covington' THEN 1
      WHEN 'UFC Fight Night 231 Almeida vs Lewis' THEN 2
      WHEN 'UFC on ESPN 52 Dariush vs Tsarukyan' THEN 3
      WHEN 'UFC Fight Night 232 Allen vs Craig' THEN 4
      WHEN 'UFC 151 Jones vs Henderson' THEN 5
      WHEN 'Bellator 301' THEN 6
      ELSE 7 END;
    DELETE FROM sqlite_sequence WHERE name = 'Events';
    INSERT INTO sqlite_sequence (name, seq) VALUES ('Events', 7);
    UPDATE Events SET Monitored = Id IN (1, 2, 4, 5, 6), Images = '[]', Description = NULL, Status = CASE WHEN Id = 5 THEN 'postponed' ELSE 'completed' END;
    INSERT INTO EventFiles (EventId, FilePath, Size, Quality, Added, \"Exists\", Languages)
      VALUES (2, '/events/UFC/Season 2023/UFC Fight Night 231 Almeida vs Lewis.mkv', 1, 'WEBDL-720p', '2023-11-06 00:00:00', 1, '[]');
    UPDATE Events SET HasFile = 1, FilePath = '/events/UFC/Season 2023/UFC Fight Night 231 Almeida vs Lewis.mkv', Quality = 'WEBDL-720p' WHERE Id = 2;
    UPDATE Leagues SET Monitored = (Name = 'UFC'), Description = NULL, LogoUrl = NULL, BannerUrl = NULL, PosterUrl = NULL,
      LastUpdate = '2099-01-01 00:00:00', MetadataLastSyncedAt = '2099-01-01 00:00:00';
    UPDATE QualityDefinitions SET MinSize = '0';
    VACUUM;"
  sed -i.bak -e 's#<SearchCacheDuration>[0-9]*</SearchCacheDuration>#<SearchCacheDuration>0</SearchCacheDuration>#' \
    -e 's#</Config>#  <CustomMetadataApiUrl>http://127.0.0.1:9/api/v2/json</CustomMetadataApiUrl>\
</Config>#' "$ARR_SEED/sportarr/config.xml"
  rm -f "$ARR_SEED/sportarr/config.xml.bak"

  log "seed written to $ARR_SEED"
}

main "$@"
