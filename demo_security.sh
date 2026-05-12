#!/bin/bash

# Configuration
CP_URL="http://localhost:5000"  # Control Plane
DP_URL="http://localhost:5001"  # Data Plane
JWT_SECRET="super-secret-key-for-interview-demo-32-bytes"

# Colors for presentation
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo "=========================================================="
echo "      Project Talaria: Security & Delivery Demo"
echo "=========================================================="

# Step 1: Identity & Authentication
# In a real scenario, this would come from an IAM provider. 
# Here we simulate a valid internal user for account ACC001.
echo -e "\n[1/5] Step: Identity Verification"
echo "Generating secure JWT for Account: ACC001 (User: DemoUser)"

# Generate a real JWT from the dev token endpoint
# This simulates the "Handshake" between Auth Provider and Control Plane.
JWT_RESPONSE=$(curl -sf "${CP_URL}/dev/token?account_id=ACC001" 2>/dev/null)
BEARER_TOKEN=$(echo "$JWT_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null)

if [ -z "$BEARER_TOKEN" ]; then
    echo -e "${RED}✗ Failed to obtain JWT. Is the Control Plane running?${NC}"
    exit 1
fi
echo "JWT obtained (first 40 chars): ${BEARER_TOKEN:0:40}..."

# Step 2: API Key Management (Persistence & Hashing)
echo -e "\n[2/5] Step: API Key Management"
echo "Creating a new API key for automation..."
# Note: The system stores only the KeyHash, never the plain key.
API_KEY_RESPONSE=$(curl -s -X POST "${CP_URL}/api/apikeys" \
  -H "Authorization: Bearer ${BEARER_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "Interview-Demo-Key", "expiresInDays": 30}')

PLAIN_KEY=$(echo $API_KEY_RESPONSE | grep -o '"key":"[^"]*' | cut -d'"' -f4)
echo -e "${GREEN}✓ Created API Key:${NC} $PLAIN_KEY (Stored as SHA256 hash in DB)"

# Step 3: Metadata Access (Authorization)
echo -e "\n[3/5] Step: Secure Metadata Retrieval"
echo "Fetching available statements for ACC001..."
STATEMENTS=$(curl -s -X GET "${CP_URL}/api/statements" \
  -H "Authorization: Bearer ${BEARER_TOKEN}")

# Extract first document ID
DOC_ID=$(echo $STATEMENTS | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)
echo -e "${GREEN}✓ Found Statement ID:${NC} $DOC_ID"

# Step 4: The Security Handshake (One-Time Access Token)
echo -e "\n[4/5] Step: Generating One-Time Download Token"
# The Control Plane verifies identity then generates a short-lived AccessToken.
DOWNLOAD_INFO=$(curl -s -X GET "${CP_URL}/api/statements/${DOC_ID}/download" \
  -H "Authorization: Bearer ${BEARER_TOKEN}")

DOWNLOAD_TOKEN=$(echo $DOWNLOAD_INFO | grep -o '"token":"[^"]*' | cut -d'"' -f4)
STREAM_URL=$(echo $DOWNLOAD_INFO | grep -o '"streamUrl":"[^"]*' | cut -d'"' -f4)

echo -e "Control Plane generated Token: ${GREEN}$DOWNLOAD_TOKEN${NC}"
echo -e "Directing user to Data Plane: ${GREEN}$STREAM_URL${NC}"

# Step 5: Verified Download & Token Burning
echo -e "\n[5/5] Step: Verified Delivery & 'Token Burning'"

echo "Attempting first download (Expected: Success)..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "${STREAM_URL}" -H "Authorization: Bearer ${DOWNLOAD_TOKEN}")

if [ "$HTTP_CODE" == "200" ]; then
    echo -e "${GREEN}✓ Download Successful!${NC} (Audit log entry created: TokenBurned)"
else
    echo -e "${RED}✗ Download Failed (Code: $HTTP_CODE)${NC}"
fi

echo "Attempting second download with SAME token (Expected: Unauthorized)..."
# This demonstrates the 'Burn on Use' logic to prevent link sharing/theft.
HTTP_CODE_REPLAY=$(curl -s -o /dev/null -w "%{http_code}" "${STREAM_URL}" -H "Authorization: Bearer ${DOWNLOAD_TOKEN}")

if [ "$HTTP_CODE_REPLAY" == "401" ] || [ "$HTTP_CODE_REPLAY" == "403" ]; then
    echo -e "${GREEN}✓ Security Verified:${NC} Replay attack blocked. Token was successfully 'burned'."
else
    echo -e "${RED}⚠️ Security Risk:${NC} Token was allowed to be reused (Code: $HTTP_CODE_REPLAY)"
fi

echo -e "\n=========================================================="
echo "      Demo Complete: Secure Persistence & Delivery"
echo "=========================================================="
