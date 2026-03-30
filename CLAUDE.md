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
- **Angular 21** with TypeScript 5.9 (standalone components, zoneless, OnPush)
- **UI**: Custom glassmorphism design system with 33 custom components — no external UI frameworks
- **Icons**: @ng-icons/core + @ng-icons/tabler-icons
- **Design System**: 3-layer SCSS (`_variables` -> `_tokens` -> `_themes`), dark/light themes
- **State Management**: @ngrx/signals (Angular signals-based)
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
│   ├── frontend/                        # Angular 21 application
│   ├── e2e/                             # Playwright E2E tests
│   ├── Dockerfile                       # Multi-stage Docker build
│   ├── entrypoint.sh                    # Docker entrypoint
│   └── Makefile                         # Build & migration helpers
├── docs/                                # Docusaurus documentation
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

### Frontend (TypeScript/Angular)
- All components must be **standalone** with **ChangeDetectionStrategy.OnPush**
- Use `input()` / `output()` function APIs (not `@Input()` / `@Output()` decorators)
- Use Angular **signals** for reactive state (`signal()`, `computed()`, `effect()`)
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
- No frontend unit tests currently

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
cd code/backend
dotnet test
```

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
- **Database migrations** require awareness of all three contexts (Data, Events, Users)
- **Malware blocker** is a critical security feature - changes require careful testing
- **Cross-seed integration** allows keeping torrents that are actively seeding
- **Real-time updates** use SignalR - maintain websocket patterns when adding features
- Use `@ng-icons/core` + `@ng-icons/tabler-icons` for icons (NOT `angular-tabler-icons` which doesn't support Angular 21)
- **Sidebar** stays dark purple in both themes - uses sidebar-specific CSS variables
- The project uses **Clean Architecture** - respect layer boundaries
- **Settings dirty tracking** uses JSON snapshot comparison (`buildSnapshot()` + `hasPendingChanges()`)
