# Cleanuparr - Claude AI Rules

## 🚨 Critical Guidelines

**READ THIS FIRST:**
1. ⚠️ **DO NOT break existing functionality** - All features are critical and must continue to work
2. ❓ **When in doubt, ASK** - Always clarify before implementing uncertain changes
3. 📋 **Follow existing patterns** - Study the codebase style before making changes
4. 🆕 **Ask before introducing new patterns** - Use current coding standards or get approval first

## Project Overview

Cleanuparr is a tool for automating the cleanup of unwanted or blocked files in Sonarr, Radarr, Lidarr, Readarr, Whisparr and supported download clients like qBittorrent, Transmission, Deluge, and µTorrent. It provides malware protection, automated cleanup, and queue management for *arr applications.

**Key Features:**
- Strike system for bad downloads
- Malware detection and blocking
- Automatic search triggering after removal
- Orphaned download cleanup with cross-seed support
- Support for multiple notification providers (Discord, etc.)

## Architecture & Tech Stack

### Backend
- **.NET 10.0** (C#) with ASP.NET Core
- **Architecture**: Clean Architecture pattern
  - `Cleanuparr.Domain` - Domain models and business logic
  - `Cleanuparr.Application` - Application services and use cases
  - `Cleanuparr.Infrastructure` - External integrations (*arr apps, download clients)
  - `Cleanuparr.Persistence` - Data access with EF Core (SQLite)
  - `Cleanuparr.Api` - REST API and web host
  - `Cleanuparr.Shared` - Shared utilities
- **Database**: SQLite with Entity Framework Core 10.0
  - Two separate contexts: `DataContext` and `EventsContext`
- **Key Libraries**:
  - MassTransit (messaging)
  - Quartz.NET (scheduling)
  - Serilog (logging)
  - SignalR (real-time communication)

### Frontend
- **Angular 19** with TypeScript 5.7
- **UI Framework**: PrimeNG 19 (with PrimeFlex and PrimeIcons)
- **State Management**: @ngrx/signals
- **Real-time Updates**: SignalR (@microsoft/signalr)
- **PWA**: Service Worker support enabled

### Documentation
- **Docusaurus** (TypeScript-based static site)
- Hosted at https://cleanuparr.github.io/Cleanuparr/

### Deployment
- **Docker** (primary distribution method)
- Standalone executables for Windows, macOS, and Linux
- Platform installers for Windows (.exe) and macOS (.pkg)

## Development Setup

### Prerequisites
- .NET 10.0 SDK
- Node.js 18+
- Git
- (Optional) Make for database migrations
- (Optional) JetBrains Rider or Visual Studio

### GitHub Packages Authentication
Cleanuparr uses GitHub Packages for NuGet dependencies. Configure access:

```bash
dotnet nuget add source \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text \
  --name Cleanuparr \
  https://nuget.pkg.github.com/Cleanuparr/index.json
```

You need a GitHub PAT with `read:packages` permission.

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

### Running Documentation
```bash
cd docs
npm install
npm start
```
Docs run at http://localhost:3000

## Project Structure

```
Cleanuparr/
├── code/
│   ├── backend/
│   │   ├── Cleanuparr.Api/           # API entry point
│   │   ├── Cleanuparr.Application/   # Business logic layer
│   │   ├── Cleanuparr.Domain/        # Domain models
│   │   ├── Cleanuparr.Infrastructure/ # External integrations
│   │   ├── Cleanuparr.Persistence/   # Database & EF Core
│   │   ├── Cleanuparr.Shared/        # Shared utilities
│   │   └── *.Tests/                  # Unit tests
│   ├── frontend/                     # Angular 19 application
│   ├── ui/                           # Built frontend assets
│   ├── Dockerfile                    # Multi-stage Docker build
│   ├── entrypoint.sh                 # Docker entrypoint
│   └── Makefile                      # Build & migration helpers
├── docs/                             # Docusaurus documentation
├── Logo/                             # Branding assets
├── .github/workflows/                # CI/CD pipelines
├── blacklist                         # Default malware patterns
├── blacklist_permissive              # Alternative blacklist
├── whitelist                         # Safe file patterns
└── CONTRIBUTING.md                   # Contribution guidelines
```

## Code Standards & Conventions

**IMPORTANT:** Always study existing code in the relevant area before making changes. Match the existing style exactly.

### Backend (C#)
- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Add XML documentation comments for public APIs
- Write unit tests for business logic
- Use meaningful names - avoid abbreviations unless widely understood
- Keep services focused - single responsibility principle
- **Study existing service implementations before creating new ones**

### Frontend (TypeScript/Angular)
- Follow [Angular Style Guide](https://angular.io/guide/styleguide)
- Use TypeScript strict mode
- Prefer signals over traditional observables for state management
- Component naming: `{feature}.component.ts`
- Service naming: `{feature}.service.ts`
- Use PrimeNG components consistently
- **Look at similar existing components before creating new ones**

### Testing
- Write unit tests for new features and bug fixes
- Use descriptive test names that explain what is being tested
- Backend: xUnit or NUnit conventions
- Frontend: Jasmine/Karma
- **Test that existing functionality still works after changes**

### Git Commit Messages
- Use clear, descriptive messages in imperative mood
- Examples: "Add Discord notification support", "Fix memory leak in download client polling"
- Reference issue numbers when applicable: "Fix #123: Handle null response from Radarr API"

### Discovering Issues
If you encounter potential gotchas, common mistakes, or areas that need special attention during development:
- **Flag them to the maintainer immediately**
- Document them if confirmed
- Consider if they should be added to this guide

## Database Migrations

Cleanuparr uses two separate database contexts:
- **DataContext**: Main application data
- **EventsContext**: Event logging and audit trail

### Creating Migrations
From the `code` directory:

```bash
# Data migrations
make migrate-data name=YourMigrationName

# Events migrations
make migrate-events name=YourMigrationName
```

Example:
```bash
make migrate-data name=AddDownloadClientConfig
make migrate-events name=AddStrikeEvents
```

## Common Development Workflows

### Adding a New *arr Application Integration
1. Add integration in `Cleanuparr.Infrastructure/Arr/`
2. Update domain models in `Cleanuparr.Domain/`
3. Create/update services in `Cleanuparr.Application/`
4. Add API endpoints in `Cleanuparr.Api/`
5. Update frontend in `code/frontend/src/app/`
6. Document in `docs/docs/`

### Adding a New Download Client
1. Add client implementation in `Cleanuparr.Infrastructure/DownloadClients/`
2. Follow existing patterns (qBittorrent, Transmission, etc.)
3. Add configuration models to `Cleanuparr.Domain/`
4. Update API and frontend as above

### Adding a New Notification Provider
1. Add provider in `Cleanuparr.Infrastructure/Notifications/`
2. Update configuration models
3. Add UI configuration in frontend
4. Test with actual service

## Important Files

### Configuration Files
- `code/backend/Cleanuparr.Api/appsettings.json` - Backend configuration
- `code/frontend/angular.json` - Angular build configuration
- `code/Dockerfile` - Docker multi-stage build
- `docs/docusaurus.config.ts` - Documentation site config

### CI/CD Workflows
- `.github/workflows/test.yml` - Run tests
- `.github/workflows/build-docker.yml` - Build Docker images
- `.github/workflows/build-executable.yml` - Build standalone executables
- `.github/workflows/release.yml` - Create releases
- `.github/workflows/docs.yml` - Deploy documentation

### Malware Protection
- `blacklist` - Default malware file patterns (strict)
- `blacklist_permissive` - Less strict patterns
- `whitelist` - Known safe file extensions
- `whitelist_with_subtitles` - Includes subtitle formats

## Contributing Guidelines

### Before Starting Work
1. **Announce your intent** - Comment on an issue or create a new one
2. **Wait for approval** from maintainers
3. Fork the repository and create a feature branch
4. Make your changes following code standards
5. Test thoroughly (both manual and automated tests)
6. Submit a PR with clear description and testing notes

### Pull Request Requirements
- Link to related issue
- Clear description of changes
- Evidence of testing
- Updated documentation if needed
- No breaking changes without discussion

## Docker Development

### Build Local Docker Image
```bash
cd code
docker build \
  --build-arg PACKAGES_USERNAME=YOUR_GITHUB_USERNAME \
  --build-arg PACKAGES_PAT=YOUR_GITHUB_PAT \
  -t cleanuparr:local \
  -f Dockerfile .
```

### Multi-Architecture Build
```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg PACKAGES_USERNAME=YOUR_GITHUB_USERNAME \
  --build-arg PACKAGES_PAT=YOUR_GITHUB_PAT \
  -t cleanuparr:local \
  -f Dockerfile .
```

## Environment Variables

When running via Docker:
- `PORT` - API port (default: 11011)
- `PUID` - User ID for file permissions
- `PGID` - Group ID for file permissions
- `TZ` - Timezone (e.g., `America/New_York`)

## Security & Safety

- Never commit sensitive data (API keys, tokens, passwords)
- All *arr and download client credentials are stored encrypted
- The malware detection system uses pattern matching on file extensions and names
- Always validate user input on both frontend and backend
- Follow OWASP guidelines for web application security

## Additional Resources

- **Documentation**: https://cleanuparr.github.io/Cleanuparr/
- **Discord**: https://discord.gg/SCtMCgtsc4
- **GitHub Issues**: https://github.com/Cleanuparr/Cleanuparr/issues
- **Releases**: https://github.com/Cleanuparr/Cleanuparr/releases

## Working with Claude - IMPORTANT

### Core Principles
1. **When in doubt, ASK** - Don't assume, clarify with the maintainer first
2. **Don't break existing functionality** - Everything is important and needs to work
3. **Follow existing coding style** - Study the codebase patterns before making changes
4. **Use current coding standards** - If you want to introduce something new, ask first

### When Modifying Code
- **ALWAYS read existing files before suggesting changes**
- Understand the current architecture and patterns
- Prefer editing existing files over creating new ones
- Follow the established conventions in the codebase exactly
- Test changes locally when possible
- **If you're unsure about an approach, ask before implementing**

### When Adding Features
- Review similar existing features first to understand patterns
- Maintain consistency with existing UI/UX patterns
- Update both backend and frontend together
- Add/update documentation
- Consider backwards compatibility
- **Ask about architectural decisions before implementing new patterns**

### When Fixing Bugs
- Understand the root cause before proposing a fix
- **Be careful not to break other functionality** - test related areas
- Add tests to prevent regression
- Update relevant documentation if behavior changes
- Consider if other parts of the codebase might have similar issues
- **Flag any potential gotchas or issues you discover**

## Notes

- The project uses **Clean Architecture** - respect layer boundaries
- Database migrations require both contexts - don't forget EventsContext
- Frontend uses **PrimeNG** - don't introduce other UI frameworks
- All downloads from *arr apps are processed through a **strike system**
- The malware blocker is a critical security feature - changes require careful testing
- Cross-seed integration allows keeping torrents that are actively seeding
- Real-time updates use **SignalR** - maintain websocket patterns when adding features
