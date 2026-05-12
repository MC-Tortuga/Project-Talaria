#!/bin/bash
set -e

echo "============================================="
echo "     Project Talaria Demo"
echo "============================================="
echo ""

CONTROLPLANE="http://localhost:5000"
DATAPLANE="http://localhost:5001"
ACCOUNT_ID="ACC001"

echo "[1/6] Checking services..."
HEALTH_CP=$(curl -s "${CONTROLPLANE}/health/live")
HEALTH_DP=$(curl -s "${DATAPLANE}/health/live")

if [[ "$HEALTH_CP" == *"Healthy"* ]] && [[ "$HEALTH_DP" == *"Healthy"* ]]; then
    echo "✓ ControlPlane (5000): Healthy"
    echo "✓ DataPlane (5001): Healthy"
else
    echo "✗ Services not healthy"
    exit 1
fi
echo ""

echo "[2/6] Getting test token..."
TOKEN_RESPONSE=$(curl -s "${CONTROLPLANE}/dev/token?account_id=${ACCOUNT_ID}&roles=Admin")
TOKEN=$(echo "$TOKEN_RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

if [[ -z "$TOKEN" ]]; then
    echo "✗ Failed to get token"
    exit 1
fi
echo "✓ Token obtained for account: ${ACCOUNT_ID}"
echo ""
echo "Token: ${TOKEN:0:50}..."
echo ""

echo "[3/6] Listing statements..."
STATEMENTS=$(curl -s -H "Authorization: Bearer ${TOKEN}" "${CONTROLPLANE}/api/statements")
echo "$STATEMENTS" | head -20
echo ""

STATEMENT_COUNT=$(echo "$STATEMENTS" | grep -o '"id"' | wc -l)
echo "✓ Found ${STATEMENT_COUNT} statements for ${ACCOUNT_ID}"
echo ""

FIRST_DOC_ID=$(echo "$STATEMENTS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

if [[ -z "$FIRST_DOC_ID" ]]; then
    echo "✗ No statements found"
    exit 1
fi

echo "[4/6] Getting download token for document: ${FIRST_DOC_ID}"
DOWNLOAD_RESPONSE=$(curl -s -H "Authorization: Bearer ${TOKEN}" "${CONTROLPLANE}/api/statements/${FIRST_DOC_ID}/download")

STREAM_TOKEN=$(echo "$DOWNLOAD_RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
STREAM_URL=$(echo "$DOWNLOAD_RESPONSE" | grep -o '"streamUrl":"[^"]*"' | cut -d'"' -f4)

if [[ -z "$STREAM_TOKEN" ]]; then
    echo "✗ Failed to get download token"
    echo "$DOWNLOAD_RESPONSE"
    exit 1
fi
echo "✓ Download token obtained"
echo "Token: ${STREAM_TOKEN:0:50}..."
echo "Stream URL: $STREAM_URL"
echo ""

echo "[5/6] Downloading statement..."
OUTPUT_FILE="/tmp/statement-${ACCOUNT_ID}.pdf"
curl -s -H "Authorization: Bearer ${STREAM_TOKEN}" "${DATAPLANE}/stream/${FIRST_DOC_ID}" -o "$OUTPUT_FILE"

if [[ -f "$OUTPUT_FILE" ]]; then
    SIZE=$(ls -lh "$OUTPUT_FILE" | awk '{print $5}')
    echo "✓ Downloaded to: ${OUTPUT_FILE} (${SIZE})"
else
    echo "✗ Failed to download statement"
    exit 1
fi
echo ""

echo "[6/6] API Key management..."

echo "Listing API keys (authenticated)..."
LIST_RESPONSE=$(curl -s -H "Authorization: Bearer ${TOKEN}" "${CONTROLPLANE}/api/apikeys")
echo "$LIST_RESPONSE"

KEY_COUNT=$(echo "$LIST_RESPONSE" | grep -o '"id"' | wc -l)
echo "✓ Found ${KEY_COUNT} API key(s)"
echo ""
echo "Note: API key creation requires a valid user in the database."

echo "============================================="
echo "     Demo Complete!"
echo "============================================="