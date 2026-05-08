#!/usr/bin/env bash
# apply-migrations.sh
# Applies pending EF Core migrations for all services.
# Called from the CD pipeline after Container Apps are healthy.
#
# Usage:
#   CONNECTION_STRING="..." ./scripts/apply-migrations.sh
#   ./scripts/apply-migrations.sh --dry-run          # list pending migrations only

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DRY_RUN="${1:-}"

if [[ -z "${CONNECTION_STRING:-}" ]]; then
  echo "ERROR: CONNECTION_STRING environment variable must be set."
  exit 1
fi

if ! dotnet ef --version &>/dev/null 2>&1; then
  dotnet tool install --global dotnet-ef
fi

declare -A SERVICES=(
  [sfa-service]="CrmPlatform.SfaService.Infrastructure.Data.SfaDbContext"
  [css-service]="CrmPlatform.CssService.Infrastructure.Data.CssDbContext"
  [marketing-service]="CrmPlatform.MarketingService.Infrastructure.Data.MarketingDbContext"
  [analytics-service]="CrmPlatform.AnalyticsService.Infrastructure.Data.AnalyticsDbContext"
  [identity-service]="CrmPlatform.IdentityService.Infrastructure.Data.IdentityDbContext"
  [platform-admin-service]="CrmPlatform.PlatformAdminService.Infrastructure.Data.PlatformDbContext"
  [notification-service]="CrmPlatform.NotificationService.Infrastructure.Data.NotificationDbContext"
  [integration-service]="CrmPlatform.IntegrationService.Infrastructure.Data.IntegrationDbContext"
  [ai-orchestration-service]="CrmPlatform.AiOrchestrationService.Infrastructure.Data.AiDbContext"
)

for SERVICE in "${!SERVICES[@]}"; do
  CONTEXT="${SERVICES[$SERVICE]}"
  SERVICE_DIR="$REPO_ROOT/src/services/$SERVICE"

  if [[ ! -d "$SERVICE_DIR/Infrastructure/Data/Migrations" ]]; then
    echo "⏭  $SERVICE — No Migrations directory, skipping."
    continue
  fi

  if [[ "$DRY_RUN" == "--dry-run" ]]; then
    echo "📋 $SERVICE — Pending migrations:"
    dotnet ef migrations list \
      --project "$SERVICE_DIR" \
      --context "$CONTEXT" \
      --connection "$CONNECTION_STRING" \
      --no-build 2>/dev/null || echo "  (unable to list — build first)"
  else
    echo "⚙  $SERVICE — Applying migrations..."
    dotnet ef database update \
      --project "$SERVICE_DIR" \
      --context "$CONTEXT" \
      --connection "$CONNECTION_STRING"
    echo "✅ $SERVICE — Done"
  fi
done
