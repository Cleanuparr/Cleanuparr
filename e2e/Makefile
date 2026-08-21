.PHONY: up up-core up-clients up-arr up-arr-fast up-lazylibrarian down test install setup seed-arr seed-lazylibrarian

ARR_SERVICES = app keycloak nginx wiremock-indexer tracker qbittorrent sonarr radarr

LAZYLIBRARIAN_SERVICES = app keycloak nginx wiremock-indexer tracker qbittorrent lazylibrarian

setup:
	bash ./scripts/setup-test-data.sh

up: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300

up-core: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx

up-clients: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx tracker qbittorrent transmission deluge utorrent rutorrent

up-arr: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 $(ARR_SERVICES)

# Same stack, built from patched sources so the Seeker can be triggered on demand.
# The app image is built first, then `up` reuses it instead of rebuilding it clean.
up-arr-fast: down setup
	bash ./scripts/with-patches.sh docker compose -f docker-compose.e2e.yml build app
	docker compose -f docker-compose.e2e.yml up -d --remove-orphans --wait --wait-timeout 300 $(ARR_SERVICES)

up-lazylibrarian: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 $(LAZYLIBRARIAN_SERVICES)

# Rebuilds e2e/arr-seed from scratch.
# Needs internet: Sonarr reads Skyhook and Radarr reads TMDB.
seed-arr:
	bash ./scripts/seed-arr.sh

# Rebuilds e2e/lazylibrarian-seed from scratch.
# Needs internet: LazyLibrarian reads OpenLibrary.
seed-lazylibrarian:
	bash ./scripts/seed-lazylibrarian.sh

down:
	docker compose -f docker-compose.e2e.yml down -v

install:
	npm install
	npx playwright install --with-deps chromium

test:
	npx playwright test
