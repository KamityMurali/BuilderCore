# BuilderCore Implementation Notes

## Completed scope
- .NET 10 Blazor Web App (Interactive Server) with Tailwind/shadcn zinc UI
- Switchable Development / Azure Entra ID authentication
- SQLite + Dapper data access with versioned schema initialization
- Cost Codes, Vendors, Estimates, Jobs, Purchase Orders, Actual Costs, Job Cost
- Budget snapshot on job creation
- Optimistic concurrency on Estimate, Job, and Purchase Order
- SignalR `JobCostHub` with `JobCostUpdated` invalidation events
- Idempotent Development demo data seeder (`DemoDataSeeder`)
- xUnit integration tests against real SQLite databases

## Deviations
- Used a custom `DbUpdateConcurrencyException` type (SQLite has no EF Core provider) with the same user-facing message required by the spec.
- Demo Job 1 uses the expanded multi-cost-code dataset from `docs/08-demo-data.md` rather than the shorter acceptance scenario in section 17 of `ONE_PROMPT_BUILD.md`; the acceptance math is covered by automated tests and can still be exercised manually with new records.
- SignalR client package (`Microsoft.AspNetCore.SignalR.Client`) added for Blazor Job Cost refresh.

## Assumptions
- Single-user/local POC: any authenticated user may perform all business actions.
- Currency formatting uses `en-US` culture.
- Job Cost variance is commitment-based (`Budget - Committed`) per product spec.

## External configuration still required for Entra mode
Set `Authentication:Mode` to `Entra` and provide:

```json
{
  "Authentication": {
    "Mode": "Entra",
    "Entra": {
      "TenantId": "{tenant-id}",
      "ClientId": "{application-client-id}",
      "ClientSecret": "{client-secret}",
      "Instance": "https://login.microsoftonline.com/",
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc",
      "SignedOutRedirectUri": "/"
    }
  }
}
```

Configure Entra redirect and logout URLs for each environment. Use user secrets or environment variables locally; never commit secrets.

## Known POC limitations
- No role-based authorization beyond authenticated-user policy
- No estimate revisions, change orders, AP/GL, or forecasting/EAC
- Actual costs are append-only in the UI
- No multi-tenant support
- SignalR refresh requires the Job Cost tab to be open (manual browser test for two-session validation)
