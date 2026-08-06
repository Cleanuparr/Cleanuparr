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

`make up-core` starts the app, Keycloak, nginx and the WireMock servers. `make up-clients` adds opentracker and the five real torrent clients (qBittorrent, Transmission, Deluge, uTorrent, ruTorrent). Both run `scripts/setup-test-data.sh` first, which writes the qBittorrent config the tests authenticate against.

## How It Works

1. **Docker Compose** starts Keycloak (with a pre-configured realm), the Cleanuparr app, nginx and four WireMock servers standing in for the *arr, download client, notification and blocklist endpoints
2. **Playwright `globalSetup`** (`tests/global-setup.ts`) waits for Keycloak, the app and WireMock to come up
3. **Each spec folder is its own Playwright project**, paired with a `setup:<folder>` project (`tests/_setup/<folder>.setup.ts`) that runs first via `dependencies`. The setup restarts the `app` container — whose `/config` is a tmpfs, so state is wiped — re-creates the admin, and writes fresh tokens to `playwright/.auth/admin.json`. The folder is the isolation boundary; specs within a folder cooperate
4. **`tests/oidc/`** holds the numbered OIDC UI specs, and its setup project enables OIDC against the Keycloak realm before they run

## CI

E2E tests do not run automatically. Trigger them on a pull request by commenting `/e2e`, which dispatches `.github/workflows/e2e.yml` through `.github/workflows/pr-build.yml`. The result is posted back as a PR comment; reports land in the `e2e-test-results-core` and `e2e-test-results-clients` artifacts of that run.

Only the accounts in the allowlist in `.github/workflows/pr-build.yml` can trigger a run. A command from any other account does nothing and gets no reply. Ask a maintainer to run the suite for you.

The suite runs as two matrix legs split by service dependency: `core` covers the 13 folders that need only the app and its mocks, and `clients` covers `download-cleaner` and `malware-blocker`, which drive the real torrent clients.
