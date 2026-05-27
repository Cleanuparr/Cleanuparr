#!/usr/bin/env bash
#
# Prepare the e2e/test-data tree before `docker compose up`.
#
# Re-creates the qBittorrent config from scratch on every run
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEST_DATA="$HERE/test-data"

mkdir -p \
  "$TEST_DATA/downloads/qbittorrent" \
  "$TEST_DATA/downloads/transmission" \
  "$TEST_DATA/downloads/deluge" \
  "$TEST_DATA/downloads/utorrent" \
  "$TEST_DATA/downloads/rtorrent" \
  "$TEST_DATA/qbittorrent-config/qBittorrent" \
  "$TEST_DATA/transmission-config" \
  "$TEST_DATA/deluge-config" \
  "$TEST_DATA/utorrent-config" \
  "$TEST_DATA/rutorrent-config"

chmod -R a+rwX "$TEST_DATA" 2>/dev/null || true

# qBittorrent credentials: admin / adminadmin
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

echo "test-data ready under $TEST_DATA"
