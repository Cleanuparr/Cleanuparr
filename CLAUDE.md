# Cleanuparr - Claude AI Rules

## Rules

1. **DO NOT break existing functionality** - All features are critical and must continue to work
2. **When in doubt, ASK** - Don't assume, clarify with the maintainer first
3. **Always read existing code before making changes** - Understand the current architecture and patterns
4. **Follow existing patterns** - Study the codebase style and match it exactly
5. **Ask before introducing new patterns** - Use current coding standards or get approval first
6. **Prefer editing existing files over creating new ones** - Build on existing work
7. **Flag potential gotchas or issues immediately** - Document and report anything unexpected
8. **If unsure about an approach, ask before implementing**

## Project Overview

Cleanuparr is a tool for automating the cleanup of unwanted or blocked files in Sonarr, Radarr, Lidarr, Readarr, Whisparr and supported download clients (qBittorrent, Transmission, Deluge, uTorrent, rTorrent). It provides malware protection, automated cleanup, and queue management for *arr applications.

**Key Features:**
- Strike system for bad downloads
- Malware detection and blocking
- Automatic search triggering after removal (Seeker)
- Missing and upgrade search
- Orphaned download cleanup with cross-seed support
- Authentication (OIDC, 2FA)
- Notification providers (Apprise, Discord, Gotify, Notifiarr, Ntfy, Pushover, Telegram)

## Architecture & Tech Stack

### Backend
- **.NET 10.0** (C#) with ASP.NET Core
- **Architecture**: Clean Architecture with `Features/` subdirectory pattern
  - `Cleanuparr.Api` - REST API and web host (`Features/` for endpoint groups)
  - `Cleanuparr.Application` - Application services and use cases
  - `Cleanuparr.Domain` - Domain models (Entities, Enums, Exceptions)
  - `Cleanuparr.Infrastructure` - External integrations (`Features/` for Arr, DownloadClient, Notifications, etc.)
  - `Cleanuparr.Persistence` - Data access with EF Core (SQLite)
  - `Cleanuparr.Shared` - Shared utilities
- **Database**: SQLite with Entity Framework Core
  - Three separate contexts: `DataContext`, `EventsContext`, `UsersContext`
- **Key Libraries**:
  - MassTransit (messaging)
  - Quartz.NET (scheduling)
  - Serilog (logging)
  - SignalR (real-time communication)
- **Testing**: xUnit + NSubstitute + Shouldly
  - Always use **NSubstitute** for mocking in new tests (Moq is being phased out)

### Frontend
- **Angular 22** with TypeScript 6.0, Node 26 (standalone components, zoneless, OnPush)
- **UI**: Custom glassmorphism design system with 33 custom components — no external UI frameworks
- **Icons**: @ng-icons/core + @ng-icons/tabler-icons
- **Design System**: 3-layer SCSS (`_variables` -> `_tokens` -> `_themes`), dark/light themes
- **State Management**: Angular signals (`signal`/`computed`/`effect`) — `@ngrx/signals` was removed (it was unused)
- **Data fetching**: Angular 22 Resource API — `rxResource` from `@angular/core/rxjs-interop` (not manual `HttpClient.subscribe()`)
- **Forms**: Angular 22 Signal Forms — `form()` + `[formField]` from `@angular/forms/signals` (settings forms; a few not-yet-migrated forms still use per-field signals)
- **Real-time Updates**: @microsoft/signalr 10.0.0
- **PWA**: Service Worker support enabled

## Project Structure

```
Cleanuparr/
├── code/
│   ├── backend/
│   │   ├── Cleanuparr.Api/              # REST API (Features/ for endpoint groups)
│   │   ├── Cleanuparr.Api.Tests/        # API layer tests
│   │   ├── Cleanuparr.Application/      # Business logic layer
│   │   ├── Cleanuparr.Domain/           # Domain models
│   │   ├── Cleanuparr.Infrastructure/   # External integrations (Features/ subdirs)
│   │   ├── Cleanuparr.Infrastructure.Tests/
│   │   ├── Cleanuparr.Persistence/      # SQLite data access
│   │   ├── Cleanuparr.Persistence.Tests/
│   │   └── Cleanuparr.Shared/           # Shared utilities
│   ├── frontend/                        # Angular 22 application
│   ├── Dockerfile                       # Multi-stage Docker build
│   ├── entrypoint.sh                    # Docker entrypoint
│   └── Makefile                         # Build & migration helpers
├── docs/                                # Docusaurus documentation
├── e2e/                                 # Playwright E2E tests
├── .github/workflows/                   # CI/CD pipelines
├── blacklist                            # Default malware patterns (strict)
├── blacklist_permissive                 # Less strict malware patterns
├── whitelist                            # Safe file extensions
└── whitelist_with_subtitles             # Includes subtitle formats
```

## Code Standards & Conventions

**IMPORTANT:** Always study existing code in the relevant area before making changes. Match the existing style exactly.

### Backend (C#)
- Follow Microsoft C# Coding Conventions
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Add XML documentation comments for public APIs
- Use meaningful names - avoid abbreviations unless widely understood
- Keep services focused - single responsibility principle
- New integrations go under `Features/` subdirectories (e.g., `Infrastructure/Features/Arr/`)
- **One type per file** - every class, record, struct and enum lives in its own file, named after it
- **Split as you go** - when a change touches a file holding several types, split that file as part of the change
- Exception: a test double used by a single spec may stay nested in that spec; doubles shared across specs go in `TestHelpers/`

### Frontend (TypeScript/Angular)
- All components must be **standalone** with **ChangeDetectionStrategy.OnPush**
- Use `input()` / `output()` function APIs (not `@Input()` / `@Output()` decorators)
- Use Angular **signals** for reactive state (`signal()`, `computed()`, `effect()`)
- **Data fetching**: use the **Resource API** (`rxResource`) with a reactive `params` + `stream`, not manual `HttpClient.subscribe()`; drive spinners/errors off `isLoading()`/`error()`
- **Forms**: use **Signal Forms** (`form()` + `[formField]`) with a single model signal + schema validators; keep the JSON-snapshot dirty tracking (`buildSnapshot()`/`hasPendingChanges()`), do NOT use Signal Forms `dirty()` for the unsaved-changes guard
- Follow the 3-layer SCSS design system (`_variables` -> `_tokens` -> `_themes`)
- **Do not introduce external UI frameworks** (no PrimeNG, Material, Tailwind, etc.)
- Component naming: `{feature}.component.ts`
- Service naming: `{feature}.service.ts`
- **Look at similar existing components before creating new ones**

### Testing
- **Backend**: xUnit + NSubstitute + Shouldly
- Always use **NSubstitute** for mocking (Moq is being phased out)
- Write unit tests for new features and bug fixes
- Use descriptive test names that explain what is being tested
- **Frontend**: Vitest via the `@angular/build:unit-test` builder in jsdom (`cd code/frontend && npm test`). Specs live next to the source as `{feature}.component.spec.ts`
- Vitest globals are enabled in `tsconfig.spec.json`, so do NOT import `describe`/`it`/`expect`/`vi`
- `angular.json` sets `skipTests: true` for all schematics, so `ng generate` never creates a spec. Write them by hand
- Test components through `TestBed.createComponent` and the rendered DOM. For inputs/outputs, declare a standalone host component in the spec. Stub API classes with a plain object of methods returning `of(...)`, never mock `HttpClient` or use `provideHttpClientTesting`
- Keep stub observables synchronous: an `rxResource` fed by `of(...)` resolves inside one `fixture.detectChanges()`, an async source needs `await fixture.whenStable()`
- Call `fixture.detectChanges()` after every interaction (zoneless + OnPush). For a bare `effect()`, use `TestBed.runInInjectionContext()` then `TestBed.tick()`

### Git Commit Messages
- Use clear, descriptive messages in imperative mood
- Examples: "Add Discord notification support", "Fix memory leak in download client polling"
- Reference issue numbers when applicable: "Fix #123: Handle null response from Radarr API"

## Development Setup

### Running the Backend
```bash
cd code/backend
dotnet build Cleanuparr.Api/Cleanuparr.Api.csproj
dotnet run --project Cleanuparr.Api/Cleanuparr.Api.csproj
```
API runs at http://localhost:5000

### Running the Frontend
```bash
cd code/frontend
npm install
npm start
```
UI runs at http://localhost:4200

### Running Tests
```bash
# Backend
cd code/backend
dotnet test

# Frontend
cd code/frontend
npm test            # ng test; watches in an interactive terminal, single run when not a TTY
npm run test:ci     # single run with coverage, written to coverage/ui/ (what CI runs)
npm run lint
```
Both run as concurrent `backend` and `frontend` jobs in `.github/workflows/test.yml`.

## Database Migrations

Three separate database contexts, all commands run from the `code` directory:

```bash
# Data migrations (DataContext)
make migrate-data name=YourMigrationName

# Events migrations (EventsContext)
make migrate-events name=YourMigrationName

# Users migrations (UsersContext)
make migrate-users name=YourMigrationName
```

## Common Development Workflows

### Adding a New *arr Application Integration
1. Add integration in `Cleanuparr.Infrastructure/Features/Arr/`
2. Update domain models in `Cleanuparr.Domain/`
3. Create/update services in `Cleanuparr.Application/`
4. Add API endpoints in `Cleanuparr.Api/Features/Arr/`
5. Update frontend in `code/frontend/src/app/`

### Adding a New Download Client
1. Add client implementation in `Cleanuparr.Infrastructure/Features/DownloadClient/`
2. Follow existing patterns (qBittorrent, Transmission, etc.)
3. Add configuration models to `Cleanuparr.Domain/`
4. Update API and frontend as above

### Adding a New Notification Provider
1. Add provider in `Cleanuparr.Infrastructure/Features/Notifications/`
2. Update configuration models
3. Add UI configuration in frontend

## Key Gotchas

- **Custom glassmorphism design system** - Do not introduce external UI frameworks (no PrimeNG, Material, Tailwind)
- **All frontend components** must be standalone with OnPush change detection
- **Frontend tests are zoneless**: `fakeAsync`/`tick()`/`flush()` from `@angular/core/testing` require Zone.js and will throw. Use `vi.useFakeTimers()` + `vi.advanceTimersByTime()`, restore with `vi.useRealTimers()`
- **`@angular/build`'s unit-test runner sets `isolate: false`** (Vitest's own default is `isolate: true`, so its docs will tell you the opposite): module state, `localStorage`, fake timers and `document.documentElement` attributes/inline styles leak between spec files. Undo them in `afterEach`
- **Node >= 25 shadows jsdom's `localStorage`**: vitest skips copying globals that already exist on `globalThis`, so specs would get an inert object (or `undefined` on Node 26). `src/testing/test-setup.ts` restores jsdom's Storage and is wired via `setupFiles` in `angular.json`. `matchMedia`, `IntersectionObserver`, `ResizeObserver` and `navigator.clipboard` are still absent in jsdom, stub them per spec
- **Frontend toolchain is pinned high**: Node >= 26 and npm 11.6.2. CI uses `setup-node@v7` with `node-version: '26'`
- **Database migrations** require awareness of all three contexts (Data, Events, Users)
- **Malware blocker** is a critical security feature - changes require careful testing
- **Cross-seed integration** allows keeping torrents that are actively seeding
- **Real-time updates** use SignalR - maintain websocket patterns when adding features
- Use `@ng-icons/core` + `@ng-icons/tabler-icons` for icons (NOT `angular-tabler-icons` which doesn't support Angular 22)
- **Sidebar** stays dark purple in both themes - uses sidebar-specific CSS variables
- The project uses **Clean Architecture** - respect layer boundaries
- **Settings dirty tracking** uses JSON snapshot comparison (`buildSnapshot()` + `hasPendingChanges()`) — keep this even with Signal Forms; Signal Forms `dirty()` means "touched", not "differs from saved"
- **Resource API** (`rxResource`): `value()` throws in the error state — always set a `defaultValue` (lists) or guard with `hasValue()` before reading
- **Signal Forms** (`[formField]`) owns `min`/`max`/`disabled`/`required` — set these via schema validators, not template bindings. Custom controls satisfy the contract via `model()` signals (`chip-input` exposes a `value` model; `size-input`'s numeric-min input is named `minValue` to avoid clashing with the field min)
