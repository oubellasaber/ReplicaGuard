# Security and Secret Management

## Do not commit secrets

This repository must never contain real credentials in tracked files, including:

- `src/ReplicaGuard.Api/appsettings.Development.json`
- `src/ReplicaGuard.Api/appsettings.json`
- `src/ReplicaGuard.Api/Properties/launchSettings.json`

Use placeholders only in committed config files.

## Local development (recommended): .NET User Secrets

`ReplicaGuard.Api` already defines a `UserSecretsId`, so local secrets can be stored outside the repository.

From the solution root, run:

```powershell
dotnet user-secrets --project .\src\ReplicaGuard.Api set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=replicaguard;Username=postgres;Password=<your-password>"
dotnet user-secrets --project .\src\ReplicaGuard.Api set "Jwt:Key" "<your-strong-base64-key>"
```

## Environment variables (.env / CI / containers)

You can also inject settings with environment variables:

- `ConnectionStrings__Database`
- `Jwt__Key`

This is preferred for Docker and CI/CD.

## Self-hosted production guidance

For self-hosted deployments, keep secrets outside the repo and inject them at runtime using:

- environment variables,
- container/runtime secrets,
- or your preferred self-hosted secret manager.

Example environment variable names:

- `ConnectionStrings__Database`
- `Jwt__Key`

## Pre-publication checklist

Before pushing public:

1. Confirm no real values exist in `appsettings*.json`.
2. Rotate any previously exposed secrets (JWT signing keys, database credentials, API keys).
3. Verify CI/CD injects secrets through secure variables or vault integration.
