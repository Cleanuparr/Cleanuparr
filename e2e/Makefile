.PHONY: up up-api up-dc down test install setup

setup:
	bash ./scripts/setup-test-data.sh

up: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans

up-api: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans app keycloak nginx

up-dc: down setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans app keycloak nginx qbittorrent transmission deluge utorrent rutorrent

down:
	docker compose -f docker-compose.e2e.yml down -v

install:
	npm install
	npx playwright install chromium

test:
	npx playwright test
