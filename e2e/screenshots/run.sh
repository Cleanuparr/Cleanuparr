#!/bin/bash
# Docs screenshot pipeline orchestrator.
#
# Builds the frontend, publishes the backend to serve it, seeds a fresh temp
# config dir (if code/backend/seed-demo-data.cs exists), starts the backend,
# runs capture.mts against it, converts the shots to WebP, then tears everything down.
#
# Env vars:
#   PORT         backend port (default 11092)
#   OUT_DIR      where screenshots are written (default docs/static/img/screenshots)
#   SKIP_BUILD=1 reuse the previous frontend/backend build for faster iteration
#   SEED=0       skip seeding even if seed-demo-data.cs exists
#   VERSION      version shown in the UI (default: latest git tag)
#
# Any args are forwarded to capture.mts as a shot filter, e.g.:
#   ./run.sh logs dashboard
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PORT="${PORT:-11092}"
OUT_DIR="${OUT_DIR:-$ROOT/docs/static/img/screenshots}"
PUBLISH_DIR="$ROOT/e2e/screenshots/.cache/publish"
SEED="${SEED:-1}"
VERSION="${VERSION:-$(git -C "$ROOT" describe --tags --abbrev=0 | sed 's/^v//')}"

CONFIG_DIR=""
BACKEND_PID=""

cleanup() {
  if [[ -n "$BACKEND_PID" ]] && kill -0 "$BACKEND_PID" 2>/dev/null; then
    kill "$BACKEND_PID" 2>/dev/null || true
    wait "$BACKEND_PID" 2>/dev/null || true
  fi
  if [[ -n "$CONFIG_DIR" && -d "$CONFIG_DIR" ]]; then
    rm -rf "$CONFIG_DIR"
  fi
}
trap cleanup EXIT

if ! command -v cwebp >/dev/null 2>&1; then
  echo "cwebp is required (brew install webp)." >&2
  exit 1
fi

if lsof -iTCP:"$PORT" -sTCP:LISTEN >/dev/null 2>&1; then
  echo "Port $PORT is already in use, aborting." >&2
  exit 1
fi

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  echo "==> Building frontend"
  (cd "$ROOT/code/frontend" && npm run build)

  echo "==> Publishing backend"
  rm -rf "$PUBLISH_DIR"
  dotnet publish "$ROOT/code/backend/Cleanuparr.Api/Cleanuparr.Api.csproj" -c Release -o "$PUBLISH_DIR" -p:Version="$VERSION"

  echo "==> Copying frontend build into wwwroot"
  rm -rf "$PUBLISH_DIR/wwwroot"
  cp -r "$ROOT/code/frontend/dist/ui/browser" "$PUBLISH_DIR/wwwroot"
else
  echo "==> SKIP_BUILD=1, reusing $PUBLISH_DIR"
  if [[ ! -f "$PUBLISH_DIR/Cleanuparr.dll" ]]; then
    echo "No previous build found at $PUBLISH_DIR. Run once without SKIP_BUILD first." >&2
    exit 1
  fi
fi

CONFIG_DIR="$(mktemp -d)"
echo "==> Config dir: $CONFIG_DIR"

SEED_SCRIPT="$ROOT/code/backend/seed-demo-data.cs"
if [[ "$SEED" == "1" && -f "$SEED_SCRIPT" ]]; then
  echo "==> Seeding demo data"
  dotnet run "$SEED_SCRIPT" -- "$CONFIG_DIR"
elif [[ "$SEED" == "1" ]]; then
  echo "==> Seed script not found at $SEED_SCRIPT yet, starting with an unseeded instance"
fi

echo "==> Starting backend on port $PORT"
# cwd must be the publish dir, or static files 401 on macOS (Program.cs WebRootFileProvider)
(
  cd "$PUBLISH_DIR" && \
  exec env CLEANUPARR_CONFIG_PATH="$CONFIG_DIR" PORT="$PORT" dotnet ./Cleanuparr.dll \
    > "$CONFIG_DIR/backend.log" 2>&1
) &
BACKEND_PID=$!

echo "==> Waiting for backend readiness"
ready=0
for _ in $(seq 1 60); do
  if curl -fsS "http://localhost:$PORT/health" >/dev/null 2>&1; then
    ready=1
    break
  fi
  if ! kill -0 "$BACKEND_PID" 2>/dev/null; then
    echo "Backend process exited early. Log:" >&2
    cat "$CONFIG_DIR/backend.log" >&2
    exit 1
  fi
  sleep 1
done

if [[ "$ready" != "1" ]]; then
  echo "Backend did not become ready in time. Log:" >&2
  cat "$CONFIG_DIR/backend.log" >&2
  exit 1
fi
echo "==> Backend ready"

echo "==> Capturing screenshots to $OUT_DIR"
mkdir -p "$OUT_DIR"
BASE_URL="http://localhost:$PORT" OUT_DIR="$OUT_DIR" \
  node "$ROOT/e2e/screenshots/capture.mts" "$@"

echo "==> Converting to WebP"
for png in "$OUT_DIR"/*.png; do
  cwebp -quiet -q 90 -m 6 "$png" -o "${png%.png}.webp"
  rm "$png"
done

echo "==> Done"
