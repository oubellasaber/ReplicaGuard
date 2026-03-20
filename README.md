# ReplicaGuard

ReplicaGuard is a self-hosted `.NET 8` backend for managing asset replication across multiple hosters.

## What it does

- Manages assets and replicas
- Schedules and executes upload jobs
- Integrates with multiple hoster providers
- Tracks upload state, retries, and failures

## Tech stack

- `.NET 8`
- ASP.NET Core Web API
- Entity Framework Core + PostgreSQL
- MassTransit messaging

## Getting started

### Prerequisites

- `.NET 8 SDK`
- PostgreSQL

### Run

```powershell
dotnet restore
dotnet build
dotnet run --project .\ReplicaGuard.Api
```

The API starts with Swagger enabled in development.

## Configuration

Required configuration is read from standard ASP.NET Core configuration sources (`appsettings`, environment variables, user secrets).

At minimum, set:

- `ConnectionStrings:Database`
- `Jwt:Key`

## Notes

- Development seeding/migrations run in Development environment.
- Keep secrets out of tracked files.
    