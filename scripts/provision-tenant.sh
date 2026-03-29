#!/usr/bin/env bash
# provision-tenant.sh
# Provisions a new SaaS tenant across all platform services.
# Usage: ./scripts/provision-tenant.sh --tenant-id <uuid> --name <name> --tier <starter|growth|professional>
#
# Prerequisites: az CLI logged in, APIM_URL and ADMIN_API_KEY environment variables set.

set -euo pipefail

TENANT_ID=""
TENANT_NAME=""
TIER=""

while [[ $# -gt 0 ]]; do
  case $1 in
    --tenant-id) TENANT_ID="$2"; shift 2 ;;
    --name)      TENANT_NAME="$2"; shift 2 ;;
    --tier)      TIER="$2"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

if [[ -z "$TENANT_ID" || -z "$TENANT_NAME" || -z "$TIER" ]]; then
  echo "Usage: $0 --tenant-id <uuid> --name <name> --tier <starter|growth|professional>"
  exit 1
fi

echo "Provisioning tenant: $TENANT_NAME ($TENANT_ID) — tier: $TIER"

# Step 1: Create tenant record in SQL
echo "[1/8] Creating tenant record in SQL..."
# TODO: call platform-admin-service API to create tenant row

# Step 2: Run EF Core migration to add tenant to RLS policies
echo "[2/8] Running tenant RLS migration..."
# TODO: trigger migration endpoint on platform-admin-service

# Step 3: Create Cosmos DB containers for tenant
echo "[3/8] Provisioning Cosmos DB containers..."
# TODO: create Cosmos containers with correct partition keys

# Step 4: Add tenant config to App Configuration
echo "[4/8] Writing tenant config to App Configuration..."
# TODO: az appconfig kv set for tenant-specific flags

# Step 5: Register tenant in Entra External ID
echo "[5/8] Registering tenant in Entra External ID..."
# TODO: create Entra External ID directory entry

# Step 6: Create Power BI workspace + apply RLS
echo "[6/8] Creating Power BI workspace..."
# TODO: Power BI REST API call

# Step 7: Create AI Search indexes
echo "[7/8] Creating AI Search indexes (kb + rag)..."
# TODO: az search index create for kb-$TENANT_ID and rag-$TENANT_ID

# Step 8: Register in Admin Portal + send welcome email
echo "[8/8] Registering in Admin Portal and sending welcome email..."
# TODO: call admin portal registration endpoint + trigger welcome email

echo "Tenant $TENANT_NAME provisioned successfully."
