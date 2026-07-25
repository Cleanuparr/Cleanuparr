.DEFAULT_GOAL := no-default

no-default:
	$(error You must specify a make target)

migrate-data:
ifndef name
	$(error name is required. Usage: make migrate-data name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context DataContext --project backend/Cleanuparr.Persistence.Sqlite/Cleanuparr.Persistence.Sqlite.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Data

migrate-events:
ifndef name
	$(error name is required. Usage: make migrate-events name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context EventsContext --project backend/Cleanuparr.Persistence.Sqlite/Cleanuparr.Persistence.Sqlite.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Events

migrate-users:
ifndef name
	$(error name is required. Usage: make migrate-users name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context UsersContext --project backend/Cleanuparr.Persistence.Sqlite/Cleanuparr.Persistence.Sqlite.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Users

PG_DUMMY = DATABASE_PROVIDER=postgres POSTGRES_HOST=localhost POSTGRES_PORT=5432 POSTGRES_USER=postgres POSTGRES_PASS=postgres POSTGRES_DB=cleanuparr

migrate-postgres-data:
ifndef name
	$(error name is required. Usage: make migrate-postgres-data name=YourMigrationName)
endif
	$(PG_DUMMY) dotnet ef migrations add $(name) --context DataContext --project backend/Cleanuparr.Persistence.Postgres/Cleanuparr.Persistence.Postgres.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Data

migrate-postgres-events:
ifndef name
	$(error name is required. Usage: make migrate-postgres-events name=YourMigrationName)
endif
	$(PG_DUMMY) dotnet ef migrations add $(name) --context EventsContext --project backend/Cleanuparr.Persistence.Postgres/Cleanuparr.Persistence.Postgres.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Events

migrate-postgres-users:
ifndef name
	$(error name is required. Usage: make migrate-postgres-users name=YourMigrationName)
endif
	$(PG_DUMMY) dotnet ef migrations add $(name) --context UsersContext --project backend/Cleanuparr.Persistence.Postgres/Cleanuparr.Persistence.Postgres.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Users

release-check:
	@previous=$$(gh release view --json tagName --jq .tagName) || exit 1; \
	published=$$(gh release view --json publishedAt --jq .publishedAt); \
	echo "Previous release: $$previous ($$published)"; \
	echo ""; \
	if ! breaking=$$(gh api "repos/{owner}/{repo}/issues?labels=breaking&state=closed&since=$$published&per_page=100" \
		--jq ".[] | select(.pull_request.merged_at > \"$$published\") | \"  #\(.number) \(.title)\""); then \
		echo "Failed to query pull request labels:" >&2; \
		echo "$$breaking" >&2; \
		exit 1; \
	fi; \
	if [ -n "$$breaking" ]; then \
		echo "Breaking changes merged since $$previous:"; \
		echo "$$breaking"; \
		echo ""; \
		echo "=> the next release must bump the MINOR"; \
	else \
		echo "No breaking changes merged since $$previous"; \
		echo ""; \
		echo "=> the next release can be a PATCH"; \
	fi

release-notes:
ifndef version
	$(error version is required. Usage: make release-notes version=2.11.0)
endif
	@previous=$$(gh release view --json tagName --jq .tagName); \
	gh api -X POST repos/{owner}/{repo}/releases/generate-notes \
		-f tag_name=v$(version) -f target_commitish=main -f previous_tag_name=$$previous --jq .body

docker-build:
ifndef tag
	$(error tag is required. Usage: make docker-build tag=latest version=1.0.0 user=... pat=...)
endif
ifndef version
	$(error version is required. Usage: make docker-build tag=latest version=1.0.0 user=... pat=...)
endif
ifndef user
	$(error user is required. Usage: make docker-build tag=latest version=1.0.0 user=... pat=...)
endif
ifndef pat
	$(error pat is required. Usage: make docker-build tag=latest version=1.0.0 user=... pat=...)
endif
	DOCKER_BUILDKIT=1 docker build \
		--build-arg VERSION=$(version) \
		--build-arg PACKAGES_USERNAME=$(user) \
		--build-arg PACKAGES_PAT=$(pat) \
		-t cleanuparr:$(tag) \
		.