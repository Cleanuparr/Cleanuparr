#!/usr/bin/env bash
#
# Applies e2e/patches to the working tree, runs a command, then reverts.
#
# The shipped build refuses to trigger the Seeker on demand, so the live-arr-fast
# suite builds an image without that guard.
# The unpatched live-arr suite keeps the real scheduled path covered.
#
# The revert runs from a trap, so an aborted build cannot leave the tree dirty.
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO="$(cd "$HERE/.." && pwd)"

shopt -s nullglob
PATCHES=("$HERE"/patches/*.patch)
shopt -u nullglob

if [ ${#PATCHES[@]} -eq 0 ]; then
  echo "[with-patches] no patches found" >&2
  exit 1
fi

revert() {
  local i
  for ((i = ${#PATCHES[@]} - 1; i >= 0; i--)); do
    git -C "$REPO" apply -R "${PATCHES[$i]}" 2>/dev/null || true
  done
  echo "[with-patches] reverted"
}

# Refuse to start unless every patch applies cleanly, so nothing is half-applied.
for patch in "${PATCHES[@]}"; do
  if ! git -C "$REPO" apply --check "$patch"; then
    echo "[with-patches] $patch does not apply, regenerate it" >&2
    exit 1
  fi
done

trap revert EXIT

for patch in "${PATCHES[@]}"; do
  git -C "$REPO" apply "$patch"
  echo "[with-patches] applied $(basename "$patch")"
done

"$@"
