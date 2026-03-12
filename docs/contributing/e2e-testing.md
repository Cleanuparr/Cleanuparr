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
- Node.js 18+
- GitHub Packages credentials (for building the app image)

## Running Locally

```bash
cd e2e

# Start Keycloak + Cleanuparr
docker compose -f docker-compose.e2e.yml up -d --build

# Install dependencies and browser
npm install
npx playwright install chromium

# Run tests (global setup waits for services and provisions the app automatically)
npx playwright test

# Tear down
docker compose -f docker-compose.e2e.yml down
```

## How It Works

1. **Docker Compose** starts Keycloak (with a pre-configured realm) and the Cleanuparr app
2. **Playwright `globalSetup`** (`tests/global-setup.ts`) automatically waits for both services, creates an admin account, and configures OIDC settings via the API
3. **`01-oidc-link.spec.ts`** logs in with local credentials, navigates to settings, and links the account to the Keycloak user (this sets `AuthorizedSubject`)
4. **`02-oidc-login.spec.ts`** verifies the full OIDC login flow — clicking "Sign in with Keycloak" on the login page, authenticating at Keycloak, and landing on the dashboard

The link test must run before the login test because the OIDC login button only appears after `AuthorizedSubject` is set.

## CI

E2E tests run automatically on PRs that touch `code/**` or `e2e/**` via `.github/workflows/e2e.yml`.
