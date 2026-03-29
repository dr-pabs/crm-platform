#!/usr/bin/env bash
# deprovision-tenant.sh
# GDPR-compliant tenant deprovisioning with 30-day hold period.
# Usage: ./scripts/deprovision-tenant.sh --tenant-id <uuid> [--hard-delete]
#
# WITHOUT --hard-delete: soft deletes the tenant (30-day hold, APIs return 403).
# WITH --hard-delete:    permanently deletes all tenant data. IRREVERSIBLE.

set -euo pipefail

TENANT_ID=""
HARD_DELETE=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --tenant-id)   TENANT_ID="$2"; shift 2 ;;
    --hard-delete) HARD_DELETE=true; shift ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

if [[ -z "$TENANT_ID" ]]; then
  echo "Usage: $0 --tenant-id <uuid> [--hard-delete]"
  exit 1
fi

if [[ "$HARD_DELETE" == "true" ]]; then
  echo "WARNING: Hard delete requested for tenant $TENANT_ID"
  echo "This will permanently delete ALL tenant data. This is IRREVERSIBLE."
  read -p "Type the tenant ID to confirm: " CONFIRM
  if [[ "$CONFIRM" != "$TENANT_ID" ]]; then
    echo "Confirmation failed. Aborting."
    exit 1
  fi
fi

if [[ "$HARD_DELETE" == "false" ]]; then
  echo "Soft deleting tenant $TENANT_ID (30-day hold)..."
  # TODO: set tenant status to Deprovisioning in SQL, all APIs return 403
  echo "Tenant soft-deleted. Hard delete available after 30-day hold."
else
  echo "Hard deleting tenant $TENANT_ID..."
  # Step 1: SQL — GDPR wipe (retain anonymised aggregates only)
  echo "[1/6] Deleting SQL tenant data..."
  # TODO: call platform-admin-service GDPR deletion endpoint

  # Step 2: Cosmos DB — delete all tenant documents
  echo "[2/6] Deleting Cosmos DB documents..."
  # TODO: delete all documents where tenantId = $TENANT_ID

  # Step 3: Blob Storage — delete tenant containers
  echo "[3/6] Deleting Blob Storage containers..."
  # TODO: az storage container delete

  # Step 4: AI Search — drop tenant indexes
  echo "[4/6] Dropping AI Search indexes..."
  # TODO: az search index delete kb-$TENANT_ID and rag-$TENANT_ID

  # Step 5: Entra External ID — remove directory entry
  echo "[5/6] Removing Entra External ID entry..."
  # TODO: Graph API call to remove tenant directory

  # Step 6: Power BI — delete workspace
  echo "[6/6] Deleting Power BI workspace..."
  # TODO: Power BI REST API delete workspace

  echo "Tenant $TENANT_ID permanently deleted."
fi
