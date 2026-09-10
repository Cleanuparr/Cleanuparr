#!/usr/bin/env bash
#
# Prints the e2e suite matrix as JSON for the CI workflow to consume.
#
# It lives here rather than inline in .github/workflows/e2e.yml so that adding or
# retiming a leg is a change to a checked-out file. A workflow triggered by an
# issue comment runs the default branch's workflow file, so an inline matrix can
# only change by merging.
#
set -euo pipefail

suites=()

# These folders need only the app, Keycloak, nginx and the wiremocks.
suites+=('{
  "name": "core",
  "make-target": "up-core",
  "projects": "--project=account --project=arr --project=auth --project=blacklist-sync --project=core --project=general --project=notifications --project=oidc --project=queue-cleaner --project=regression --project=seeker --project=signalr"
}')

# Same stack, but the app is built from patched sources so the Seeker can be
# triggered instead of waited for. See e2e/patches.
suites+=('{
  "name": "seeker-fast",
  "make-target": "up-core-fast",
  "projects": "--project=seeker-fast"
}')

# These folders need the torrent clients and the tracker.
suites+=('{
  "name": "clients",
  "make-target": "up-clients",
  "projects": "--project=download-cleaner --project=download-client --project=malware-blocker"
}')

# This folder needs the real arrs, the fake indexer and qBittorrent.
suites+=('{
  "name": "live-arr",
  "make-target": "up-arr",
  "projects": "--project=live-arr"
}')

# Same stack as live-arr, patched like seeker-fast.
suites+=('{
  "name": "live-arr-fast",
  "make-target": "up-arr-fast",
  "projects": "--project=live-arr-fast"
}')

# This folder needs the real LazyLibrarian, the fake indexer and qBittorrent.
suites+=('{
  "name": "live-lazylibrarian",
  "make-target": "up-lazylibrarian",
  "projects": "--project=live-lazylibrarian"
}')

# jq compacts it to the single line an Actions output needs, and rejects a malformed entry.
printf '[%s]' "$(IFS=,; printf '%s' "${suites[*]}")" | jq -c .
