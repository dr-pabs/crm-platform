#!/usr/bin/env bash
# generate-migrations.sh
# Generates EF Core InitialCreate migrations for all services that have a DbContext.
# Run this script once from the repo root after initial setup.
# Requires: dotnet SDK + dotnet-ef tool installed.
#
# Usage:
#   ./scripts/generate-migrations.sh
#   ./scripts/generate-migrations.sh --service sfa-service   # single service only

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MIGRATION_NAME="${MIGRATION_NAME:-InitialCreate}"

# Install dotnet-ef if not available
if ! dotnet ef --version &>/dev/null 2>&1; then
  echo "Installing dotnet-ef tool..."
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

FILTER="${1:-}"
SERVICE_FILTER=""
if [[ "$FILTER" == "--service" ]]; then
  SERVICE_FILTER="${2:-}"
fi

for SERVICE in "${!SERVICES[@]}"; do
  if [[ -n "$SERVICE_FILTER" && "$SERVICE" != "$SERVICE_FILTER" ]]; then
    continue
  fi

  CONTEXT="${SERVICES[$SERVICE]}"
  SERVICE_DIR="$REPO_ROOT/src/services/$SERVICE"
  MIGRATIONS_DIR="$SERVICE_DIR/Infrastructure/Data/Migrations"

  if [[ -d "$MIGRATIONS_DIR" ]]; then
    echo "⏭  $SERVICE — Migrations already exist, skipping. Use --force to regenerate."
    continue
  fi

  echo "⚙  $SERVICE — Generating $MIGRATION_NAME..."
  dotnet ef migrations add "$MIGRATION_NAME" \
    --project "$SERVICE_DIR" \
    --context "$CONTEXT" \
    --output-dir "Infrastructure/Data/Migrations"

  echo "✅ $SERVICE — Done"
done

echo ""
echo "Migration generation complete."
echo "Commit the generated Migrations/ directories, then run apply-migrations.sh to apply."
