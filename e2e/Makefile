.PHONY: up up-core up-core-fast up-clients up-arr up-arr-fast up-lazylibrarian \
	up-stack-core up-stack-clients up-stack-arr up-stack-lazylibrarian up-app \
	down test install setup seed-arr seed-lazylibrarian

COMPOSE = docker compose -f docker-compose.e2e.yml
WAIT = --remove-orphans --wait --wait-timeout 150

# The app reaches these through depends_on, so a stack started without it must name them.
WIREMOCKS = wiremock-arr wiremock-dlc wiremock-notify wiremock-blocklist

# nginx depends_on app, so naming it would drag the app into the stack half.
APP_SERVICES = app nginx

CORE_STACK = keycloak $(WIREMOCKS)
CLIENTS_STACK = $(CORE_STACK) tracker qbittorrent transmission deluge utorrent rutorrent
ARR_STACK = $(CORE_STACK) wiremock-indexer tracker qbittorrent sonarr radarr
LAZYLIBRARIAN_STACK = $(CORE_STACK) wiremock-indexer tracker qbittorrent lazylibrarian

setup:
	bash ./scripts/setup-test-data.sh

up: down setup
	$(COMPOSE) up -d --build $(WAIT)

up-core: down setup
	$(COMPOSE) up -d --build $(WAIT) $(APP_SERVICES) $(CORE_STACK)

# Same stack as up-core, built from patched sources so the Seeker can be triggered on demand.
up-core-fast: down setup
	bash ./scripts/with-patches.sh $(COMPOSE) build app
	$(COMPOSE) up -d $(WAIT) $(APP_SERVICES) $(CORE_STACK)

up-clients: down setup
	$(COMPOSE) up -d --build $(WAIT) $(APP_SERVICES) $(CLIENTS_STACK)

up-arr: down setup
	$(COMPOSE) up -d --build $(WAIT) $(APP_SERVICES) $(ARR_STACK)

# Same stack as up-arr, patched like up-core-fast.
up-arr-fast: down setup
	bash ./scripts/with-patches.sh $(COMPOSE) build app
	$(COMPOSE) up -d $(WAIT) $(APP_SERVICES) $(ARR_STACK)

up-lazylibrarian: down setup
	$(COMPOSE) up -d --build $(WAIT) $(APP_SERVICES) $(LAZYLIBRARIAN_STACK)

# CI raises the stack while a separate job is still building the app image.
up-stack-core: down setup
	$(COMPOSE) up -d $(WAIT) $(CORE_STACK)

up-stack-clients: down setup
	$(COMPOSE) up -d $(WAIT) $(CLIENTS_STACK)

up-stack-arr: down setup
	$(COMPOSE) up -d $(WAIT) $(ARR_STACK)

up-stack-lazylibrarian: down setup
	$(COMPOSE) up -d $(WAIT) $(LAZYLIBRARIAN_STACK)

up-app:
	$(COMPOSE) up -d $(WAIT) $(APP_SERVICES)

# Rebuilds e2e/arr-seed from scratch.
# Needs internet: Sonarr reads Skyhook and Radarr reads TMDB.
seed-arr:
	bash ./scripts/seed-arr.sh

# Rebuilds e2e/lazylibrarian-seed from scratch.
# Needs internet: LazyLibrarian reads OpenLibrary.
seed-lazylibrarian:
	bash ./scripts/seed-lazylibrarian.sh

down:
	$(COMPOSE) down -v

install:
	npm install
	npx playwright install --with-deps chromium

test:
	npx playwright test
