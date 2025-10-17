# Contributing to Cleanuparr

Thanks for your interest in contributing to Cleanuparr! This guide will help you get started with development.

## Before You Start

### Announce Your Intent

Before starting any work, please let us know what you want to contribute:

- For existing issues: Comment on the issue stating you'd like to work on it
- For new features/changes: Create a new issue first and mention that you want to work on it

This helps us avoid redundant work, git conflicts, and contributions that may not align with the project's direction.

**Wait for approval from the maintainers before proceeding with your contribution.**

## Development Setup

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [Git](https://git-scm.com/)
- (Optional) [Make](https://www.gnu.org/software/make/) for database migrations
- (Optional) IDE: [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio](https://visualstudio.microsoft.com/)

### Repository Setup

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Cleanuparr.git
   cd Cleanuparr
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/Cleanuparr/Cleanuparr.git
   ```

## Backend Development

### Initial Setup

#### 1. Create a GitHub Personal Access Token (PAT)

Cleanuparr uses GitHub Packages for NuGet dependencies. You'll need a PAT with `read:packages` permission:

1. Go to [GitHub Settings > Developer Settings > Personal Access Tokens > Tokens (classic)](https://github.com/settings/tokens)
2. Click "Generate new token" → "Generate new token (classic)"
3. Give it a descriptive name (e.g., "Cleanuparr NuGet Access")
4. Set an expiration (recommend 90 days or longer for development)
5. Select only the `read:packages` scope
6. Click "Generate token" and copy it

#### 2. Configure NuGet Source

Add the Cleanuparr NuGet repository:

```bash
dotnet nuget add source \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text \
  --name Cleanuparr \
  https://nuget.pkg.github.com/Cleanuparr/index.json
```

Replace `YOUR_GITHUB_USERNAME` and `YOUR_GITHUB_PAT` with your GitHub username and the PAT you created.

### Running the Backend

#### Option 1: Using .NET CLI

Navigate to the backend directory:
```bash
cd code/backend
```

Build the application:
```bash
dotnet build Cleanuparr.Api/Cleanuparr.Api.csproj
```

Run the application:
```bash
dotnet run --project Cleanuparr.Api/Cleanuparr.Api.csproj
```

Run tests:
```bash
dotnet test
```

The API will be available at http://localhost:5000

#### Option 2: Using an IDE

For JetBrains Rider or Visual Studio:
1. Open the solution file: `code/backend/cleanuparr.sln`
2. Set `Cleanuparr.Api` as the startup project
3. Press `F5` to start the application

### Database Migrations

Cleanuparr uses two separate database contexts: `DataContext` and `EventsContext`.

#### Prerequisites

Install Make if not already installed:
- Windows: Install via [Chocolatey](https://chocolatey.org/) (`choco install make`) or use [WSL](https://docs.microsoft.com/windows/wsl/)
- macOS: Install via Homebrew (`brew install make`)
- Linux: Usually pre-installed, or install via package manager (`apt install make`, `yum install make`, etc.)

#### Creating Migrations

From the `code` directory:

For data migrations (DataContext):
```bash
make migrate-data name=YourMigrationName
```

For events migrations (EventsContext):
```bash
make migrate-events name=YourMigrationName
```

Example:
```bash
make migrate-data name=AddUserPreferences
make migrate-events name=AddAuditLogEvents
```

## Frontend Development

### Setup

1. Navigate to the frontend directory:
   ```bash
   cd code/frontend
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm start
   ```

The UI will be available at http://localhost:4200

## Documentation Development

### Setup

1. Navigate to the docs directory:
   ```bash
   cd docs
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm start
   ```

The documentation site will be available at http://localhost:3000

## Building with Docker

### Building a Local Docker Image

To build the Docker image locally for testing:

1. Navigate to the `code` directory:
   ```bash
   cd code
   ```

2. Build the image:
   ```bash
   docker build \
     --build-arg PACKAGES_USERNAME=YOUR_GITHUB_USERNAME \
     --build-arg PACKAGES_PAT=YOUR_GITHUB_PAT \
     -t cleanuparr:local \
     -f Dockerfile .
   ```

   Replace `YOUR_GITHUB_USERNAME` and `YOUR_GITHUB_PAT` with your credentials.

3. Run the container:
   ```bash
   docker run -d \
     --name cleanuparr-dev \
     -p 11011:11011 \
     -v /path/to/config:/config \
     -e PUID=1000 \
     -e PGID=1000 \
     -e TZ=Etc/UTC \
     cleanuparr:local
   ```

4. Access the application at http://localhost:11011

### Building for Multiple Architectures

Use Docker Buildx for multi-platform builds:

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg PACKAGES_USERNAME=YOUR_GITHUB_USERNAME \
  --build-arg PACKAGES_PAT=YOUR_GITHUB_PAT \
  -t cleanuparr:local \
  -f Dockerfile .
```

## Code Standards

### Backend (.NET/C#)
- Follow existing conventions and [Microsoft C# Coding Conventions](https://docs.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Write unit tests whenever possible

### Frontend (Angular/TypeScript)
- Follow existing conventions and the [Angular Style Guide](https://angular.io/guide/styleguide)
- Use TypeScript strict mode
- Write unit tests whenever possible

### Documentation
- Use clear, concise language
- Include code examples where appropriate
- Update relevant documentation when adding/changing features
- Check for spelling and grammar

## Submitting Your Contribution

### 1. Create a Feature Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/your-bug-fix-name
```

### 2. Make Your Changes

- Write clean, well-documented code
- Follow the code standards outlined above
- **Test your changes thoroughly!**

### 3. Commit Your Changes

Write clear, descriptive commit messages:

```bash
git add .
git commit -m "Add feature: brief description of your changes"
```

### 4. Keep Your Branch Updated

```bash
git fetch upstream
git rebase upstream/main
```

### 5. Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### 6. Create a Pull Request

1. Go to the [Cleanuparr repository](https://github.com/Cleanuparr/Cleanuparr)
2. Click "New Pull Request"
3. Select your fork and branch
4. Fill out the PR template with:
   - A descriptive title (e.g., "Add support for Prowlarr integration" or "Fix memory leak in download client polling")
   - Description of changes
   - Related issue number
   - Testing performed
   - Screenshots (if applicable)

### 7. Code Review Process

- Maintainers will review your PR
- Address any feedback or requested changes
- Once approved, your PR will be merged

## Other Ways to Contribute

### Help Test New Features

We're always looking for testers to help validate new features before they are released. If you'd like to help test upcoming changes:

1. Join our [Discord community](https://discord.gg/SCtMCgtsc4)
2. Let us know you're interested in testing
3. We'll provide you with pre-release builds and testing instructions

Your feedback helps us catch issues early and deliver better releases.

## Getting Help

- Discord: Join our [Discord community](https://discord.gg/SCtMCgtsc4) for real-time help
- Issues: Check existing [GitHub issues](https://github.com/Cleanuparr/Cleanuparr/issues) or create a new one
- Documentation: Review the [complete documentation](https://cleanuparr.github.io/Cleanuparr/)

## License

By contributing to Cleanuparr, you agree that your contributions will be licensed under the same license as the project.

---

Thanks for contributing to Cleanuparr!
