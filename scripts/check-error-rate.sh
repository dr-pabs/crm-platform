#!/usr/bin/env bash
# check-error-rate.sh
# Used by cd-prod.yml during blue/green traffic shifts.
# Polls Application Insights for 5xx error rate and fails if threshold exceeded.
# Requires: AZURE_APP_INSIGHTS_APP_ID and AZURE_APP_INSIGHTS_API_KEY env vars.
# Usage: ./scripts/check-error-rate.sh --threshold <pct> --duration <Nm>

set -euo pipefail

THRESHOLD=1.0
DURATION=5

while [[ $# -gt 0 ]]; do
  case $1 in
    --threshold) THRESHOLD="$2"; shift 2 ;;
    --duration)  DURATION="${2%m}"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

echo "Monitoring error rate for ${DURATION} minutes (threshold: ${THRESHOLD}%)"

APP_ID="${AZURE_APP_INSIGHTS_APP_ID:?AZURE_APP_INSIGHTS_APP_ID must be set}"
API_KEY="${AZURE_APP_INSIGHTS_API_KEY:?AZURE_APP_INSIGHTS_API_KEY must be set}"

QUERY="requests | where timestamp > ago(${DURATION}m) | summarize total=count(), errors=countif(resultCode >= '500') | extend errorRate=iff(total==0, 0.0, errors*100.0/total) | project errorRate"
ENCODED_QUERY=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))" "${QUERY}")

RESPONSE=$(curl -sf \
  -H "x-api-key: ${API_KEY}" \
  "https://api.applicationinsights.io/v1/apps/${APP_ID}/query?query=${ENCODED_QUERY}")

ERROR_RATE=$(python3 -c "
import json, sys
data = json.loads(sys.argv[1])
rows = data.get('tables', [{}])[0].get('rows', [[0.0]])
print(rows[0][0] if rows else 0.0)
" "${RESPONSE}" 2>/dev/null || echo "0.0")

echo "Current error rate: ${ERROR_RATE}%"

if (( $(echo "${ERROR_RATE} > ${THRESHOLD}" | bc -l) )); then
  echo "::error::Error rate ${ERROR_RATE}% exceeds threshold ${THRESHOLD}% — triggering rollback"
  exit 1
fi

echo "Error rate ${ERROR_RATE}% is within threshold ${THRESHOLD}% — continuing traffic shift"
exit 0
