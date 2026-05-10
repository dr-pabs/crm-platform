# CRM Platform — Developer Makefile
# See ADR 0012 and docs/local-development.md for full local dev guide.
#
# Usage:
#   make dev          Start full local stack (all services + infrastructure)
#   make dev-sfa      Start infrastructure + identity + SFA service only
#   make dev-infra    Start infrastructure only (SQL, Service Bus, Azurite, Mailpit)
#   make migrate      Apply EF Core migrations to local SQL
#   make seed         Re-seed development data
#   make reset        Tear down and rebuild local environment from scratch
#   make stop         Stop everything
#   make test-local   Run all tests against local infrastructure
#   make lint         Run dotnet format on all services
#   make help         Show this help

.PHONY: help dev dev-sfa dev-css dev-marketing dev-infra migrate seed reset stop test-local test-unit test-integration lint

# Service port map (matches ADR 0012)
IDENTITY_PORT   := 5001
PLATFORM_PORT   := 5002
SFA_PORT        := 5010
CSS_PORT        := 5020
MARKETING_PORT  := 5030
ANALYTICS_PORT  := 5040
AI_PORT         := 5050
BFF_PORT        := 5060
NOTIFICATION_PORT := 5070
AUTH_STUB_PORT  := 5100
STAFF_PORT      ?= 3000

# Local SQL credentials (local dev only — never used in Azure)
SQL_SA_PASSWORD := Dev_Password_123!
SQL_HOST        := localhost
SQL_PORT        := 1433

help: ## Show available make targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# ============================================================
# Infrastructure
# ============================================================

dev-infra: ## Start infrastructure only (SQL, Service Bus, Azurite, Mailpit)
	@echo "🐳 Starting local infrastructure..."
	docker compose up -d
	@echo "⏳ Waiting for SQL Server to be healthy..."
	@until docker exec crm-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(SQL_SA_PASSWORD)" -Q "SELECT 1" -C > /dev/null 2>&1; do sleep 2; done
	@echo "✅ Infrastructure ready."
	@echo "   SQL Server:        localhost:$(SQL_PORT)"
	@echo "   Service Bus AMQP:  localhost:5672"
	@echo "   Service Bus Mgmt:  http://localhost:8080"
	@echo "   Azurite Blob:      http://localhost:10000"
	@echo "   Mailpit UI:        http://localhost:8025"

# ============================================================
# Full stack
# ============================================================

dev: dev-infra migrate ## Start full local stack (infrastructure + all services)
	@echo "🚀 Starting all services..."
	dotnet run --project src/services/identity-service --no-launch-profile --verbosity quiet --urls http://localhost:$(IDENTITY_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/platform-admin-service --no-launch-profile --verbosity quiet --urls http://localhost:$(PLATFORM_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/sfa-service --no-launch-profile --verbosity quiet --urls http://localhost:$(SFA_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/css-service --no-launch-profile --verbosity quiet --urls http://localhost:$(CSS_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/marketing-service --no-launch-profile --verbosity quiet --urls http://localhost:$(MARKETING_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/analytics-service --no-launch-profile --verbosity quiet --urls http://localhost:$(ANALYTICS_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/ai-orchestration-service --no-launch-profile --verbosity quiet --urls http://localhost:$(AI_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/notification-service --no-launch-profile --verbosity quiet --urls http://localhost:$(NOTIFICATION_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/_local/auth-stub --no-launch-profile --verbosity quiet --urls http://localhost:$(AUTH_STUB_PORT) >/dev/null 2>&1 &
	@if [ -f src/frontend/staff-portal/package.json ]; then \
		echo "🌐 Starting frontend..."; \
		if [ ! -d src/frontend/staff-portal/node_modules ]; then \
			echo "📦 Installing staff portal dependencies..."; \
			cd src/frontend/staff-portal && npm install; \
		fi; \
		cd src/frontend/staff-portal && npm run dev -- --host 0.0.0.0 --port $(STAFF_PORT) >/dev/null 2>&1 & \
	else \
		echo "⚠️  Staff portal not initialized (no package.json found)"; \
	fi
	@echo ""
	@echo "✅ Full stack started. Service ports:"
	@echo "   identity-service:          http://localhost:$(IDENTITY_PORT)"
	@echo "   platform-admin-service:    http://localhost:$(PLATFORM_PORT)"
	@echo "   sfa-service:               http://localhost:$(SFA_PORT)"
	@echo "   css-service:               http://localhost:$(CSS_PORT)"
	@echo "   marketing-service:         http://localhost:$(MARKETING_PORT)"
	@echo "   analytics-service:         http://localhost:$(ANALYTICS_PORT)"
	@echo "   ai-orchestration-service:  http://localhost:$(AI_PORT)"
	@echo "   notification-service:      http://localhost:$(NOTIFICATION_PORT)"
	@echo "   auth-stub:                 http://localhost:$(AUTH_STUB_PORT)"
	@echo "   Staff Portal:              http://localhost:$(STAFF_PORT)"

dev-sfa: dev-infra migrate ## Start infrastructure + identity + SFA service (for SFA feature work)
	@echo "🚀 Starting identity-service and sfa-service..."
	dotnet run --project src/services/identity-service --no-launch-profile --verbosity quiet --urls http://localhost:$(IDENTITY_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/_local/auth-stub --no-launch-profile --verbosity quiet --urls http://localhost:$(AUTH_STUB_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/sfa-service --no-launch-profile --urls http://localhost:$(SFA_PORT) &
	@echo "✅ SFA stack ready at http://localhost:$(SFA_PORT)"
	@echo "   Get a token: ./scripts/local/get-dev-token.sh --tenant TenantA --role SalesRep"

dev-css: dev-infra migrate ## Start infrastructure + identity + CS&S service (for CSS feature work)
	@echo "🚀 Starting identity-service and css-service..."
	dotnet run --project src/services/identity-service --no-launch-profile --verbosity quiet --urls http://localhost:$(IDENTITY_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/_local/auth-stub --no-launch-profile --verbosity quiet --urls http://localhost:$(AUTH_STUB_PORT) >/dev/null 2>&1 &
	dotnet run --project src/services/css-service --no-launch-profile --urls http://localhost:$(CSS_PORT) &
	@echo "✅ CSS stack ready at http://localhost:$(CSS_PORT)"

dev-marketing: dev-infra migrate ## Start infrastructure + identity + marketing service (for Marketing feature work)
	@echo "🚀 Starting identity-service and marketing-service..."
	dotnet run --project src/services/identity-service --no-launch-profile --urls http://localhost:$(IDENTITY_PORT) &
	dotnet run --project src/services/_local/auth-stub --no-launch-profile --urls http://localhost:$(AUTH_STUB_PORT) &
	dotnet run --project src/services/marketing-service --no-launch-profile --urls http://localhost:$(MARKETING_PORT) &
	@echo "✅ Marketing stack ready at http://localhost:$(MARKETING_PORT)"

# ============================================================
# Database
# ============================================================

migrate: ## Apply EF Core migrations to local SQL for all services
	@echo "🗄️  Running EF Core migrations..."
	@for service in identity-service platform-admin-service sfa-service css-service marketing-service analytics-service ai-orchestration-service notification-service integration-service; do \
		if [ -d "src/services/$$service" ]; then \
			echo "  Migrating $$service..."; \
			dotnet ef database update --project src/services/$$service --verbosity quiet >/dev/null 2>&1 || echo "  ⚠️  No migrations yet for $$service (expected during Phase 0)"; \
		fi; \
	done
	@echo "✅ Migrations complete."

seed: ## Re-seed development data (run after migrate)
	@echo "🌱 Seeding development data..."
	sqlcmd -S $(SQL_HOST),$(SQL_PORT) -U sa -P "$(SQL_SA_PASSWORD)" -d CrmPlatform \
		-i scripts/local/sql-init/05-seed-dev-data.sql
	@echo "✅ Dev data seeded."

reset: ## Tear down and rebuild local environment from scratch
	@echo "🔄 Resetting local environment..."
	docker compose down -v
	docker compose up -d
	@echo "⏳ Waiting for SQL Server..."
	dotnet build CrmPlatform.sln	@until docker exec crm-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$(SQL_SA_PASSWORD)" -Q "SELECT 1" -C > /dev/null 2>&1; do sleep 2; done
	$(MAKE) migrate
	$(MAKE) seed
	@echo "✅ Environment reset complete."

stop: ## Stop all local services and infrastructure
	@echo "🛑 Stopping services..."
	-pkill -f "dotnet run" 2>/dev/null || true
	-pkill -f "pnpm dev" 2>/dev/null || true
	docker compose down
	@echo "✅ All services stopped."

# ============================================================
# Testing
# ============================================================

test-unit: ## Run unit tests (no infrastructure required)
	@echo "🧪 Running unit tests..."
	dotnet test src/ --filter "Category!=Integration&Category!=TenantIsolation" \
		--collect:"XPlat Code Coverage" \
		--results-directory ./coverage/unit
	@echo "✅ Unit tests complete."

test-integration: ## Run integration tests against local SQL + Service Bus
	@echo "🧪 Running integration tests (requires dev-infra to be running)..."
	ASPNETCORE_ENVIRONMENT=Development \
	dotnet test src/ --filter "Category=Integration" \
		--collect:"XPlat Code Coverage" \
		--results-directory ./coverage/integration
	@echo "✅ Integration tests complete."

test-tenant-isolation: ## Run tenant isolation tests (CI hard gate)
	@echo "🔒 Running tenant isolation tests..."
	ASPNETCORE_ENVIRONMENT=Development \
	dotnet test src/ --filter "Category=TenantIsolation" \
		--results-directory ./coverage/tenant-isolation
	@echo "✅ Tenant isolation tests complete."

test-local: test-unit test-integration test-tenant-isolation ## Run all tests against local infrastructure
	@echo "✅ All local tests complete."

# ============================================================
# Code quality
# ============================================================

lint: ## Run dotnet format on all services (verify-no-changes for CI)
	@echo "🔍 Running dotnet format..."
	@for service in src/services/*/; do \
		if [ -d "$$service" ] && [ "$$service" != "src/services/_template/" ] && [ "$$service" != "src/services/_local/" ]; then \
			echo "  Formatting $$service..."; \
			dotnet format "$$service" --verify-no-changes 2>/dev/null || echo "  ⚠️  Format issues in $$service"; \
		fi; \
	done
	@echo "✅ Lint complete."
