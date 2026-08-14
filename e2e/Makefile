.PHONY: up up-core up-clients up-arr down test install setup seed-arr

setup:
	bash ./scripts/setup-test-data.sh

up: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300

up-core: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx

up-clients: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx tracker qbittorrent transmission deluge utorrent rutorrent

up-arr: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx wiremock-indexer tracker qbittorrent sonarr radarr

# Rebuilds e2e/arr-seed from scratch.
# Needs internet: Sonarr reads Skyhook and Radarr reads TMDB.
seed-arr:
	bash ./scripts/seed-arr.sh

down:
	docker compose -f docker-compose.e2e.yml down -v

install:
	npm install
	npx playwright install --with-deps chromium

test:
	npx playwright test
