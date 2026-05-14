#!/bin/bash
set -e

MAX_RETRIES=30
RETRY_INTERVAL=2

echo "=== Waiting for services to be healthy ==="

# Wait for MySQL
echo "Waiting for MySQL..."
for i in $(seq 1 $MAX_RETRIES); do
    docker exec talaria-mysql mysqladmin ping -h localhost -u root -prootpassword > /dev/null 2>&1 && break
    echo "Attempt $i/$MAX_RETRIES - MySQL not ready, waiting..."
    sleep $RETRY_INTERVAL
done
if [ $i -eq $MAX_RETRIES ]; then
    echo "ERROR: MySQL failed to start after $((MAX_RETRIES * RETRY_INTERVAL)) seconds"
    exit 1
fi
echo "MySQL ready"

# Wait for ControlPlane
echo "Waiting for ControlPlane..."
for i in $(seq 1 $MAX_RETRIES); do
    curl -sf http://localhost:5000/health/live > /dev/null 2>&1 && break
    echo "Attempt $i/$MAX_RETRIES - ControlPlane not ready, waiting..."
    sleep $RETRY_INTERVAL
done
if [ $i -eq $MAX_RETRIES ]; then
    echo "ERROR: ControlPlane failed to start after $((MAX_RETRIES * RETRY_INTERVAL)) seconds"
    exit 1
fi
echo "ControlPlane ready"

# Wait for Streamer
echo "Waiting for Streamer..."
for i in $(seq 1 $MAX_RETRIES); do
    curl -sf http://localhost:5001/health/live > /dev/null 2>&1 && break
    echo "Attempt $i/$MAX_RETRIES - Streamer not ready, waiting..."
    sleep $RETRY_INTERVAL
done
if [ $i -eq $MAX_RETRIES ]; then
    echo "ERROR: Streamer failed to start after $((MAX_RETRIES * RETRY_INTERVAL)) seconds"
    exit 1
fi
echo "Streamer ready"

echo ""
echo "=== All services healthy ==="
echo ""
echo "Note: Database seeding is handled automatically by the ControlPlane on startup."
echo ""
echo "Test the flow:"
echo "1. Get test token: curl 'http://localhost:5000/dev/token?account_id=ACC001'"
echo "2. Get statements: curl -H 'Authorization: Bearer <token>' http://localhost:5000/api/statements"
echo "3. Download: Use token to call /api/statements/{guid}/download"
echo "4. Stream: Use one-time token to call /stream/{guid}?token=..."