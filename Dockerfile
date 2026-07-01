# Multi-stage build: build and publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything and restore only API graph
COPY . ./
RUN dotnet restore Api/ConcurrentBooking.Api.csproj

# Publish API
RUN dotnet publish Api/ConcurrentBooking.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image: ASP.NET runtime is enough — migrations run inside the app
# on startup (MigratingDatabaseInitializer), so no SDK or source tree needed.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish ./

# Copy entrypoint script
COPY entrypoint.sh ./entrypoint.sh
RUN sed -i 's/\r$//' ./entrypoint.sh
RUN chmod +x ./entrypoint.sh

# Default port for local/container runs; managed hosts (Render) override via PORT.
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80
ENTRYPOINT ["/app/entrypoint.sh"]
