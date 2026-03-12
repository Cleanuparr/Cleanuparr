.PHONY: up down test install

up:
	docker compose -f docker-compose.e2e.yml up -d --build

down:
	docker compose -f docker-compose.e2e.yml down

install:
	npm install
	npx playwright install chromium

test:
	npx playwright test
