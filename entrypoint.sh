#!/usr/bin/env bash
set -e

echo "Starting entrypoint: applying migrations and running app"

# make sure dotnet tools path is available
export PATH="$PATH:/root/.dotnet/tools"

# Install dotnet-ef tool if not present
if ! command -v dotnet-ef >/dev/null 2>&1; then
  echo "Installing dotnet-ef tool..."
  dotnet tool install --global dotnet-ef --version 8.0.0 || true
fi

# Wait for Postgres to be ready
echo "Waiting for Postgres at $DB_HOST or via connection string"
RETRY=0
MAX_RETRIES=30
until dotnet ef database update --project /src/ConcurrentBooking.Infrastructure/ConcurrentBooking.Infrastructure.csproj --startup-project /src/Api/ConcurrentBooking.Api.csproj --no-build; do
  RETRY=$((RETRY+1))
  if [ $RETRY -ge $MAX_RETRIES ]; then
    echo "Migrations failed after $RETRY attempts"
    exit 1
  fi
  echo "Database not ready yet - waiting ($RETRY/$MAX_RETRIES)..."
  sleep 2
done

echo "Migrations applied successfully"

# Start the app
dotnet ConcurrentBooking.Api.dll
