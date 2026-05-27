.PHONY: up down test install setup

setup:
	bash ./scripts/setup-test-data.sh

up: setup
	docker compose -f docker-compose.e2e.yml up -d --build --remove-orphans

down:
	docker compose -f docker-compose.e2e.yml down -v

install:
	npm install
	npx playwright install chromium

test:
	npx playwright test
