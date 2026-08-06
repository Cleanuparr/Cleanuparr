.PHONY: up up-core up-clients down test install setup

setup:
	bash ./scripts/setup-test-data.sh

up: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300

up-core: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx

up-clients: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans --wait --wait-timeout 300 app keycloak nginx tracker qbittorrent transmission deluge utorrent rutorrent

down:
	docker compose -f docker-compose.e2e.yml down -v

install:
	npm install
	npx playwright install --with-deps chromium

test:
	npx playwright test
