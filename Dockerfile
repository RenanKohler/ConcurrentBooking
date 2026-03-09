# Multi-stage build: build and publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything and restore only API graph
COPY . ./
RUN dotnet restore Api/ConcurrentBooking.Api.csproj

# Publish API
RUN dotnet publish Api/ConcurrentBooking.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image (use SDK so migrations can run)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish ./
# Copy full source so dotnet-ef can run against projects
COPY --from=build /src /src

# Copy entrypoint script
COPY entrypoint.sh ./entrypoint.sh
RUN sed -i 's/\r$//' ./entrypoint.sh
RUN chmod +x ./entrypoint.sh

ENV DOTNET_MODIFIABLE_ASSEMBLIES=debug
ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80
ENTRYPOINT ["/app/entrypoint.sh"]
