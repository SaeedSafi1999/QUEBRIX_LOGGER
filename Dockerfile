# =============================================================================
# QUEBRIX Logger Server - Dockerfile
# =============================================================================
# Multi-stage build for production-ready deployment
# =============================================================================

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY QUEBRIX.Logger.sln .
COPY QUEBRIX.Logger.Common/*.csproj ./QUEBRIX.Logger.Common/
COPY QUEBRIX.Logger.Contracts/*.csproj ./QUEBRIX.Logger.Contracts/
COPY QUEBRIX.Logger.Storage.Abstractions/*.csproj ./QUEBRIX.Logger.Storage.Abstractions/
COPY QUEBRIX.Logger.Storage.Elasticsearch/*.csproj ./QUEBRIX.Logger.Storage.Elasticsearch/
COPY QUEBRIX.Logger.Security/*.csproj ./QUEBRIX.Logger.Security/
COPY QUEBRIX.Logger.Core/*.csproj ./QUEBRIX.Logger.Core/
COPY QUEBRIX.Logger.Server/*.csproj ./QUEBRIX.Logger.Server/
COPY QUEBRIX.Logger.Sink/*.csproj ./QUEBRIX.Logger.Sink/
COPY QUEBRIX.Logger.SDK/*.csproj ./QUEBRIX.Logger.SDK/
COPY QUEBRIX.Logger.Tests/*.csproj ./QUEBRIX.Logger.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY . .

# Build and publish the server project
WORKDIR /src/QUEBRIX.Logger.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl --no-install-recommends && rm -rf /var/lib/apt/lists/*

# Copy published artifacts
COPY --from=build /app/publish .

# Create non-root user
RUN addgroup --system --gid 1001 quebrix && \
    adduser --system --uid 1001 --ingroup quebrix quebrix && \
    chown -R quebrix:quebrix /app

USER quebrix

# Environment variables (overridable at runtime)
ENV QUEBRIX_APPLICATION="QUEBRIX Logger"
ENV QUEBRIX_ENVIRONMENT="Production"
ENV QUEBRIX_LISTENURL="http://0.0.0.0:8080"
ENV QUEBRIX_ELASTICSEARCH__URLS__0="http://elasticsearch:9200"

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "QUEBRIX.Logger.Server.dll"]