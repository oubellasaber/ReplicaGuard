# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Cache dependencies
COPY Directory.Packages.props Directory.Build.props ./
COPY src/ReplicaGuard.Api/ReplicaGuard.Api.csproj src/ReplicaGuard.Api/
COPY src/ReplicaGuard.Application/ReplicaGuard.Application.csproj src/ReplicaGuard.Application/
COPY src/ReplicaGuard.Domain/ReplicaGuard.Domain.csproj src/ReplicaGuard.Domain/
COPY src/ReplicaGuard.Infrastructure/ReplicaGuard.Infrastructure.csproj src/ReplicaGuard.Infrastructure/
COPY src/ReplicaGuard.Contracts/ReplicaGuard.Contracts.csproj src/ReplicaGuard.Contracts/

RUN dotnet restore src/ReplicaGuard.Api/ReplicaGuard.Api.csproj

# Build - copy all source
COPY . .
WORKDIR /src/src/ReplicaGuard.Api
RUN dotnet publish ReplicaGuard.Api.csproj -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false \
    /p:SelfContained=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

WORKDIR /app

RUN addgroup -g 1000 appgroup \
 && adduser -u 1000 -G appgroup -s /bin/sh -D appuser

RUN mkdir -p /app/spool /app/uploads \
 && chown -R appuser:appgroup /app

COPY --from=build --chown=appuser:appgroup /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/healthz || exit 1

USER appuser

VOLUME ["/app/spool", "/app/uploads"]

EXPOSE 8080 8081

ENTRYPOINT ["dotnet", "ReplicaGuard.Api.dll"]
