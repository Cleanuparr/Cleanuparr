#!/usr/bin/env bash
#
# Prints the e2e suite matrix as JSON for the CI workflow to consume.
#
# A workflow triggered by an issue comment runs the default branch's copy, so a
# matrix written inline in e2e.yml can only change by merging. Here it is a
# checked-out file, and a PR can add or retime a leg.
#
# stack-target raises everything except the app, so a leg can do that while the
# app image is still building. image-variant picks which image it then runs.
#
set -euo pipefail

suites=()

# These folders need only the app, Keycloak, nginx and the wiremocks.
suites+=('{
  "name": "core",
  "stack-target": "up-stack-core",
  "image-variant": "plain",
  "projects": "--project=account --project=arr --project=auth --project=blacklist-sync --project=core --project=general --project=notifications --project=oidc --project=queue-cleaner --project=regression --project=seeker --project=signalr"
}')

# Same stack, but the app is built from patched sources so the Seeker can be
# triggered instead of waited for. See e2e/patches.
suites+=('{
  "name": "seeker-fast",
  "stack-target": "up-stack-core",
  "image-variant": "patched",
  "projects": "--project=seeker-fast"
}')

# These folders need the torrent clients and the tracker.
suites+=('{
  "name": "clients",
  "stack-target": "up-stack-clients",
  "image-variant": "plain",
  "projects": "--project=download-cleaner --project=download-client --project=malware-blocker"
}')

# This folder needs the real arrs, the fake indexer and qBittorrent.
suites+=('{
  "name": "live-arr",
  "stack-target": "up-stack-arr",
  "image-variant": "plain",
  "projects": "--project=live-arr"
}')

# Same stack as live-arr, patched like seeker-fast.
suites+=('{
  "name": "live-arr-fast",
  "stack-target": "up-stack-arr",
  "image-variant": "patched",
  "projects": "--project=live-arr-fast"
}')

# This folder needs the real LazyLibrarian, the fake indexer and qBittorrent.
suites+=('{
  "name": "live-lazylibrarian",
  "stack-target": "up-stack-lazylibrarian",
  "image-variant": "plain",
  "projects": "--project=live-lazylibrarian"
}')

# jq compacts it to the single line an Actions output needs, and rejects a malformed entry.
printf '[%s]' "$(IFS=,; printf '%s' "${suites[*]}")" | jq -c .
