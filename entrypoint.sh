#!/usr/bin/env bash
set -e

# Migrations and demo seeding run inside the app on startup via
# MigratingDatabaseInitializer (BookingDbContext.Database.MigrateAsync()),
# so there is no separate dotnet-ef step here. The published app lives in
# the working directory; exec so it receives termination signals directly.
echo "Starting ConcurrentBooking API (migrations apply on startup)..."
exec dotnet ConcurrentBooking.Api.dll
