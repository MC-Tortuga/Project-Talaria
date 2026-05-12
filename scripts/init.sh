#!/bin/bash
set -e

MAX_RETRIES=30
RETRY_INTERVAL=2

echo "=== Waiting for services to be healthy ==="

# Wait for SQL Server
echo "Waiting for SQL Server..."
for i in $(seq 1 $MAX_RETRIES); do
    docker exec talaria-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -C -Q "SELECT 1" -No > /dev/null 2>&1 && break
    echo "Attempt $i/$MAX_RETRIES - SQL Server not ready, waiting..."
    sleep $RETRY_INTERVAL
done
if [ $i -eq $MAX_RETRIES ]; then
    echo "ERROR: SQL Server failed to start after $((MAX_RETRIES * RETRY_INTERVAL)) seconds"
    exit 1
fi
echo "SQL Server ready"

# Wait for LocalStack
echo "Waiting for LocalStack..."
for i in $(seq 1 $MAX_RETRIES); do
    curl -sf http://localhost:4566/_localstack/health > /dev/null 2>&1 && break
    echo "Attempt $i/$MAX_RETRIES - LocalStack not ready, waiting..."
    sleep $RETRY_INTERVAL
done
if [ $i -eq $MAX_RETRIES ]; then
    echo "ERROR: LocalStack failed to start after $((MAX_RETRIES * RETRY_INTERVAL)) seconds"
    exit 1
fi
echo "LocalStack ready"

# Wait for ControlPlane
echo "Waiting for ControlPlane..."
for i in $(seq 1 $MAX_RETRIES); do
    curl -sf http://localhost:5000/health > /dev/null 2>&1 && break
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
    curl -sf http://localhost:5001/health > /dev/null 2>&1 && break
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

# Run initialization scripts
echo "=== Running init-localstack.sh ==="
docker compose exec -T controlplane /bin/sh /app/scripts/init-localstack.sh

echo "=== Running seed-db.sh ==="
docker compose exec -T controlplane /bin/sh /app/scripts/seed-db.sh

echo ""
echo "=== Initialization complete ==="
echo ""
echo "Test the flow:"
echo "1. Get test token: curl 'http://localhost:5000/dev/token?account_id=ACC001'"
echo "2. Get statement IDs: docker exec talaria-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -d Talaria -C -Q 'SELECT Id, AccountNumber FROM BankStatements'"
echo "3. Download: Use token to call /api/statements/{guid}/download"
echo "4. Stream: Use one-time token to call /stream/{guid}?token=..."