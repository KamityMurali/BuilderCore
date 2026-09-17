# BuilderCore — Construction Estimating, Purchasing, and Job Cost POC

A working proof-of-concept for residential construction cost control:

**Cognito Sign-In → Estimate → Approve → Job/Budget → Purchase Order → Issue PO → Post Actual Cost → Job Cost/Variance → SignalR refresh**

## Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: AWS Cognito User Pool (only required for production-like authentication)

## Technology
- .NET 10, Blazor Interactive Server, SignalR
- Dapper + Microsoft.Data.Sqlite + SQLite (`buildercore.db`)
- AWS Cognito OIDC (optional) or Development auth

## Quick start (local Development)

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/BuilderCore.Web
```

Open the URL shown in the console (typically `https://localhost:7xxx`).

### Development authentication
In Development, the app uses a local authenticated user automatically:
- `sub`: `local-dev-user`
- `email`: `developer@buildercore.local`
- `name`: `POC Developer`

Configured in `src/BuilderCore.Web/appsettings.Development.json`:

```json
{
  "Authentication": { "Mode": "Development" },
  "DemoData": { "Enabled": true }
}
```

**Safety rule:** Development auth only works when `ASPNETCORE_ENVIRONMENT=Development`. Startup fails if Development auth is configured outside Development.

## Database initialization
On startup the app:
1. Creates `buildercore.db` if missing (project working directory)
2. Applies versioned scripts from `database/` (`001_schema.sql`, `002_seed.sql`)
3. Seeds realistic demo data when `DemoData:Enabled=true`

Connection string (default):

```text
Data Source=buildercore.db;Foreign Keys=True
```

No manual SQL steps are required for local POC use.

## Cognito configuration (when ready)
1. Create a Cognito User Pool, domain, and web app client
2. Enable Authorization Code flow with scopes `openid`, `email`, `profile`
3. Configure callback URL: `https://{host}/signin-oidc`
4. Configure sign-out URL: `https://{host}/`
5. Set `Authentication:Mode` to `Cognito` and populate `Authentication:Cognito` settings

Example (values via user secrets / environment variables):

```json
{
  "Authentication": {
    "Mode": "Cognito",
    "Cognito": {
      "Authority": "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_XXXXX",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "SignedOutRedirectUri": "/"
    }
  },
  "DemoData": { "Enabled": false }
}
```

Login/logout endpoints: `/login` and `/logout`.

## Demo workflow
After first launch in Development:

1. **Dashboard** — active jobs, draft estimates, open POs
2. **Estimates** — `EST-1001` (Approved), `EST-1003` (Draft)
3. **Jobs** — `JOB-1001` (Active, full demo), `JOB-1002` (negative framing variance), `JOB-1004` (Closed)
4. **Purchase Orders** — Draft, Issued, and Closed examples
5. **Job Cost** — open `JOB-1001` → Job Cost tab for Budget / Committed / Actual / Variance

Manual acceptance path (new data):
1. Create Draft estimate with lines → Save
2. Approve estimate
3. Create Job (budget snapshot)
4. Create Draft PO → verify committed unchanged
5. Issue PO → committed updates
6. Post Actual Cost → actual updates
7. Open Job Cost in two browser sessions; issue/close PO or post cost in one session and confirm the other refreshes

## Tests

```bash
dotnet test
```

Integration tests use temporary SQLite files and synthetic auth (no live Cognito calls).

## Project structure

```text
BuilderCore.sln
src/BuilderCore.Web/          Blazor app, services, Dapper repositories, SignalR hub
tests/BuilderCore.Tests/      xUnit integration tests
database/                    Schema and master seed SQL
docs/                        Product, architecture, UI, and test specifications
```

See `IMPLEMENTATION_NOTES.md` for completed scope, assumptions, and known limitations.
