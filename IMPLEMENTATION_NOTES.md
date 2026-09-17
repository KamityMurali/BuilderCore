# BuilderCore Implementation Notes

## Completed scope
- .NET 10 Blazor Web App (Interactive Server) with Bootstrap styling
- Switchable Development / Cognito authentication
- SQLite + Dapper data access with versioned schema initialization
- Cost Codes, Vendors, Estimates, Jobs, Purchase Orders, Actual Costs, Job Cost
- Budget snapshot on job creation
- Optimistic concurrency on Estimate, Job, and Purchase Order
- SignalR `JobCostHub` with `JobCostUpdated` invalidation events
- Idempotent Development demo data seeder (`DemoDataSeeder`)
- xUnit integration tests against real SQLite databases (32 tests)

## Deviations
- Used a custom `DbUpdateConcurrencyException` type (SQLite has no EF Core provider) with the same user-facing message required by the spec.
- Demo Job 1 uses the expanded multi-cost-code dataset from `docs/08-demo-data.md` rather than the shorter acceptance scenario in section 17 of `ONE_PROMPT_BUILD.md`; the acceptance math is covered by automated tests and can still be exercised manually with new records.
- SignalR client package (`Microsoft.AspNetCore.SignalR.Client`) added for Blazor Job Cost refresh.

## Assumptions
- Single-user/local POC: any authenticated user may perform all business actions.
- Currency formatting uses `en-US` culture.
- Job Cost variance is commitment-based (`Budget - Committed`) per product spec.

## External configuration still required for Cognito mode
Set `Authentication:Mode` to `Cognito` and provide:

```json
{
  "Authentication": {
    "Mode": "Cognito",
    "Cognito": {
      "Authority": "https://cognito-idp.{region}.amazonaws.com/{userPoolId}",
      "ClientId": "{app-client-id}",
      "ClientSecret": "{app-client-secret-if-required}",
      "MetadataAddress": "{optional-discovery-url}",
      "SignedOutRedirectUri": "/"
    }
  }
}
```

Configure Cognito callback URL(s) and sign-out URL(s) for each environment. Use user secrets or environment variables locally; never commit secrets.

## Known POC limitations
- No role-based authorization beyond authenticated-user policy
- No estimate revisions, change orders, AP/GL, or forecasting/EAC
- Actual costs are append-only in the UI
- No multi-tenant support
- SignalR refresh requires the Job Cost tab to be open (manual browser test for two-session validation)
