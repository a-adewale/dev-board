# DevBoard

built by toucheCodes

## Structure

```text
backend/src/DevBoard.Api/          ASP.NET Core API
backend/tests/DevBoard.Api.Tests/  API integration tests
frontend/                          React + TypeScript client
docs/                              Architecture and learning notes
```

## Run locally

Use two terminals from this folder.

```powershell
# Terminal 1
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
dotnet run --project backend\src\DevBoard.Api --urls http://localhost:5080

# Terminal 2
Set-Location frontend
npm run dev
```

Open `http://localhost:5173`. The workspace, issue board and API status should load.

## Verify

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli"
dotnet test

Set-Location frontend
npm run lint
npm run build
```

## Request flow

1. React mounts the `App` component.
2. `useEffect` sends an HTTP GET request to the API.
3. ASP.NET Core matches `/api/health` and creates a `HealthResponse`.
4. ASP.NET Core serializes the C# record to JSON.
5. React parses the JSON and renders loading, success or error state.

See `PROJECT.md` for the product scope and future milestones.

## Production hardening included

- `/api/ready` readiness endpoint
- JSON problem-details error responses
- Fixed-window API rate limiter
- Docker and Compose deployment files
- GitHub Actions build, test and lint workflow
- PostgreSQL production provider and Compose database service

The current local authentication is intentionally not enabled yet. Before exposing the app publicly, add an external OIDC provider, server-side authorization policies, secret storage and database migrations.

## Colour codes for push(still working on it)
Grey push - progress but not 100% functional 
White push - beginning stage
Black push - 
Red push - 
