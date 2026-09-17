# Local Verification Checklist

After the coding agent finishes:

## Build
```bash
dotnet restore
dotnet build
dotnet test
```

All required tests must pass.

## Run
```bash
dotnet run --project src/BuilderPOC.Web
```

## Authentication
- Application starts in Development without Cognito.
- A local development user is authenticated.
- Development authentication cannot be enabled outside Development.

## Database
- `builderpoc.db` is created automatically.
- Schema initializes successfully.
- Foreign keys are enabled.
- Demo seeding is idempotent.

## Demo data
Verify:
- Approved `EST-1001`
- Active `JOB-1001`
- Draft `EST-1003` with no Job
- Closed `JOB-1004`
- realistic vendors and cost codes
- Draft, Issued, and/or Closed purchase orders
- actual-cost transactions
- at least one negative cost-code variance

## Core workflow
Create a new Estimate and verify:
1. Save Draft.
2. Add/edit lines.
3. Approve.
4. Approved Estimate becomes read-only.
5. Create Job.
6. Job budget snapshots the Estimate.
7. Create Draft PO.
8. Draft PO does not affect committed cost.
9. Issue PO.
10. Issued PO affects committed cost.
11. Post Actual Cost.
12. Job Cost shows Budget, Committed, Actual, and Variance.

## SignalR
Open the same Job Cost page in two browser sessions:
1. In session A, issue/close a PO or post Actual Cost.
2. Session B should receive the Job Cost invalidation event.
3. Session B should re-query SQLite and refresh without a full browser reload.

## Git review
```bash
git status
git diff
```

Search for:
- TODO
- FIXME
- NotImplementedException
- hard-coded secrets
- production authentication bypasses
- accidental committed SQLite database files
