#!/usr/bin/env bash
#
# Prepare the e2e/test-data tree before `docker compose up`.
#
# Re-creates the qBittorrent config from scratch on every run
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DATA="$HERE/test-data"

ARR_SEED="$HERE/arr-seed"

# Torrents the fake Torznab indexer serves, one file per release.
# Rebuilt on every run so the tree does not grow.
rm -rf "$TEST_DATA/torznab-src"

# Media an arr imports during a run, wiped so its library always starts empty.
rm -rf "$TEST_DATA/sonarr-tv" "$TEST_DATA/radarr-movies" "$TEST_DATA/lazylibrarian-books"

mkdir -p \
  "$TEST_DATA/torznab-src" \
  "$TEST_DATA/sonarr-tv" \
  "$TEST_DATA/radarr-movies" \
  "$TEST_DATA/lazylibrarian-books" \
  "$TEST_DATA/downloads/qbittorrent" \
  "$TEST_DATA/downloads/transmission" \
  "$TEST_DATA/downloads/deluge" \
  "$TEST_DATA/downloads/utorrent" \
  "$TEST_DATA/downloads/rtorrent" \
  "$TEST_DATA/downloads/sabnzbd/incomplete" \
  "$TEST_DATA/downloads/sabnzbd/complete" \
  "$TEST_DATA/sabnews-articles" \
  "$TEST_DATA/orphaned-xdev" \
  "$TEST_DATA/qbittorrent-config/qBittorrent" \
  "$TEST_DATA/transmission-config" \
  "$TEST_DATA/deluge-config" \
  "$TEST_DATA/utorrent-config" \
  "$TEST_DATA/rutorrent-config" \
  "$TEST_DATA/sabnzbd-config"

chmod -R a+rwX "$TEST_DATA" 2>/dev/null || true

# Credentials: admin / adminadmin
cat > "$TEST_DATA/qbittorrent-config/qBittorrent/qBittorrent.conf" <<'EOF'
[LegalNotice]
Accepted=true

[Preferences]
WebUI\Port=8090
WebUI\Address=*
WebUI\CSRFProtection=false
WebUI\HostHeaderValidation=false
WebUI\LocalHostAuth=false
WebUI\AuthSubnetWhitelistEnabled=true
WebUI\AuthSubnetWhitelist=127.0.0.0/8, ::1/128
WebUI\Username=admin
WebUI\Password_PBKDF2="@ByteArray(ARQ77eY1NUZ366igo9pHIQ==:Bn3qWLqOY3qE6Z+sCx2NoO5q4nhgxhUL3eRD4Zw3+5p9C7+RmrI20bzAjcwHKqcWa+5z6QBQGckCB8sFCnVTGw==)"
Downloads\SavePath=/downloads
EOF

# Headless SABnzbd config: a config file present (with a server already
# defined) skips the first-run wizard. host/download_dir/complete_dir are
# absolute so SabnzbdService.GetClaimedPathsAsync can resolve and claim them
# (it skips a relative download_dir as unreachable from this host).
cat > "$TEST_DATA/sabnzbd-config/sabnzbd.ini" <<'EOF'
[misc]
config_version = 25
api_key = 0000000000000000000000000000e2e5
nzb_key = 0000000000000000000000000000e2e5
host = 0.0.0.0
port = 8070
download_dir = /downloads/incomplete
complete_dir = /downloads/complete
fail_hopeless_jobs = 1
enable_https = 0
check_new_rel = 0
auto_browser = 0
language = en
host_whitelist = localhost,127.0.0.1
inet_exposure = 0

[servers]
[[sabnews]]
displayname = sabnews
host = localhost
port = 8888
connections = 1
ssl = 0
enable = 1
priority = 0
# A connection to sabnews can stall for the full timeout; 20 is the lowest SABnzbd accepts.
timeout = 20

[categories]
[[*]]
pp = 3
script = Default
priority = 0
EOF

# Restore Sonarr and Radarr from the committed seed.
# The arrs write to /config on every start, so the run gets a throwaway copy.
for arr in sonarr radarr; do
  if [[ -d "$ARR_SEED/$arr" ]]; then
    rm -rf "$TEST_DATA/$arr-config"
    cp -R "$ARR_SEED/$arr" "$TEST_DATA/$arr-config"
    chmod -R a+rwX "$TEST_DATA/$arr-config"
  else
    mkdir -p "$TEST_DATA/$arr-config"
  fi
done

# Restore LazyLibrarian from its committed seed.
# LazyLibrarian rewrites config.ini when it shuts down, so the run gets a copy.
LAZYLIBRARIAN_SEED="$HERE/lazylibrarian-seed"
rm -rf "$TEST_DATA/lazylibrarian-config"
if [[ -d "$LAZYLIBRARIAN_SEED" ]]; then
  cp -R "$LAZYLIBRARIAN_SEED" "$TEST_DATA/lazylibrarian-config"
  chmod -R a+rwX "$TEST_DATA/lazylibrarian-config"
else
  mkdir -p "$TEST_DATA/lazylibrarian-config"
fi

echo "test-data ready under $TEST_DATA"
