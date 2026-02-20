.DEFAULT_GOAL := no-default

no-default:
	$(error You must specify a make target)

migrate-data:
ifndef name
	$(error name is required. Usage: make migrate-data name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context DataContext --project backend/Cleanuparr.Persistence/Cleanuparr.Persistence.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Data

migrate-events:
ifndef name
	$(error name is required. Usage: make migrate-events name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context EventsContext --project backend/Cleanuparr.Persistence/Cleanuparr.Persistence.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Events

migrate-users:
ifndef name
	$(error name is required. Usage: make migrate-users name=YourMigrationName)
endif
	dotnet ef migrations add $(name) --context UsersContext --project backend/Cleanuparr.Persistence/Cleanuparr.Persistence.csproj --startup-project backend/Cleanuparr.Api/Cleanuparr.Api.csproj --output-dir Migrations/Users

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