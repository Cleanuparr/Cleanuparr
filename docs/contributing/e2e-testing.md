# End-to-End Testing with Keycloak and Playwright

E2E tests use a real Keycloak instance and Playwright browser automation to validate full OIDC round-trips that mocked tests cannot catch.

## Test Coverage Layers

| Layer | What it catches |
|-------|----------------|
| Unit tests (`OidcAuthServiceTests`) | PKCE, URL encoding, token validation logic, expiry handling |
| Integration tests (`OidcAuthControllerTests`, `AccountControllerOidcTests`) | HTTP routing, middleware, cookie/token handling (mocked IdP) |
| **E2E tests** | Real browser redirects, actual Keycloak protocol, full OIDC round-trip |

## Prerequisites

- Docker + Docker Compose
- Node.js 26+
- GitHub Packages credentials (for building the app image)

## Running Locally

```bash
cd e2e

# Prepare test-data/ and start the stack, waiting for containers to become healthy.
# Use up-clients instead for the download-cleaner and malware-blocker specs.
make up-core

# Install dependencies and browser
make install

# Run a folder's tests (its setup project runs automatically)
npx playwright test --project=core

# Tear down
make down
```

`make up-core` starts the app, Keycloak, nginx and the WireMock servers. `make up-clients` adds opentracker and the five real torrent clients (qBittorrent, Transmission, Deluge, uTorrent, ruTorrent). `make up-arr` adds real Sonarr and Radarr containers, a fake Torznab indexer and qBittorrent, for the `live-arr` folder. All three run `scripts/setup-test-data.sh` first, which writes the qBittorrent config the tests authenticate against and restores the *arr configs.

## Live *arr Tests

`tests/live-arr/` runs the Seeker against real Sonarr and Radarr. Only the indexer is faked, because no real indexer can be shipped in the test stack. A search is a real `SeasonSearch` or `MoviesSearch` command, and the grab lands in a real qBittorrent and in the *arr's own queue.

The containers start with a library already in place. `e2e/arr-seed/` holds a committed `config.xml` and database per *arr, and `setup-test-data.sh` copies them into `test-data/` on every run. CI therefore needs no metadata lookups.

Regenerate the seed with `make seed-arr`. That is the only step needing internet: Sonarr resolves the series through Skyhook and Radarr resolves the movie through TMDB. Re-run it when the pinned `sonarr` or `radarr` image tag changes, because a seed database is only valid for the version that wrote it.

The seed holds one Sonarr series with its first season monitored, and two Radarr movies. The second movie is left unmonitored on purpose, so a spec that wants a single candidate gets one. The specs that need two monitor it themselves and put it back.

### The two live folders

`tests/live-arr` runs against the shipped image. It waits for the real cron schedule, which is the only place that path is exercised.

`tests/live-arr-fast` runs against an image built from patched sources, started with `make up-arr-fast`. The patches live in `e2e/patches`:

| Patch | What it removes |
|-------|-----------------|
| `0001-allow-triggering-the-seeker` | the guard that rejects `POST /api/jobs/Seeker/trigger` |
| `0002-drop-the-seeker-jitter` | the random delay a run waits before searching |
| `0003-shorten-the-search-command-timeout` | 30 minutes of waiting before a running command times out, cut to 20 seconds, with the monitor poll cut from 60 to 5 seconds |

Together they turn a two-to-four minute wait per test into seconds, which is what makes a behaviour matrix affordable. The third patch is what makes `seeker-timeout.api.spec.ts` possible: it holds the indexer's answer so the arr's command keeps running, then asserts the event settles on `TimedOut`.

The poll cut is not cosmetic. A spec must let the indexer answer before the arr gives up on it, because an arr that times out on an indexer benches it for about a minute, and the next spec then searches with no active indexer. The 5-second poll settles the event long before that. `scripts/with-patches.sh` applies the patches, builds, and reverts from a `trap`, so an aborted build cannot leave the tree dirty.

The split matters: the patched image is not the shipped one, so anything that depends on scheduling or timing has to stay in `tests/live-arr`. Put behaviour in `live-arr-fast` and timing in `live-arr`.

Specs in `live-arr-fast` must never rely on the schedule. Its setup project parks `searchInterval` at its 360-minute maximum, and `arrangeInstance` verifies every override it sends actually persisted, so a silently dropped setting fails loudly instead of making a test pass for the wrong reason.

## How It Works

1. **Docker Compose** starts Keycloak (with a pre-configured realm), the Cleanuparr app, nginx and four WireMock servers standing in for the *arr, download client, notification and blocklist endpoints
2. **Playwright `globalSetup`** (`tests/global-setup.ts`) waits for Keycloak, the app and WireMock to come up
3. **Each spec folder is its own Playwright project**, paired with a `setup:<folder>` project (`tests/_setup/<folder>.setup.ts`) that runs first via `dependencies`. The setup restarts the `app` container — whose `/config` is a tmpfs, so state is wiped — re-creates the admin, and writes fresh tokens to `playwright/.auth/admin.json`. The folder is the isolation boundary; specs within a folder cooperate
4. **`tests/oidc/`** holds the numbered OIDC UI specs, and its setup project enables OIDC against the Keycloak realm before they run

## CI

E2E tests do not run automatically. Trigger them on a pull request by commenting `/e2e`, which dispatches `.github/workflows/e2e.yml` through `.github/workflows/pr-build.yml`. The result is posted back as a PR comment; reports land in the `e2e-test-results-core`, `e2e-test-results-clients` and `e2e-test-results-live-arr` artifacts of that run.

Only the accounts in the allowlist in `.github/workflows/pr-build.yml` can trigger a run. A command from any other account does nothing and gets no reply. Ask a maintainer to run the suite for you.

The suite runs as four matrix legs split by service dependency: `core` covers the 13 folders that need only the app and its mocks, `clients` covers `download-cleaner` and `malware-blocker`, which drive the real torrent clients, and `live-arr` plus `live-arr-fast` cover the folders that drive real Sonarr and Radarr containers. The `live-arr-fast` leg builds the app from patched sources; every other leg builds it clean.
