# ONE-PROMPT BUILD INSTRUCTIONS — BuilderPOC

You are the senior .NET engineer responsible for implementing this entire POC end-to-end in one autonomous coding session.

## Mission
Build a working construction estimating, purchasing, and job-cost POC using:
- .NET 10
- Blazor Web App with Interactive Server rendering (Blazor Server behavior)
- SignalR
- Dapper
- Microsoft.Data.Sqlite
- SQLite (local file database)
- AWS Cognito User Pool using OpenID Connect Authorization Code flow
- Bootstrap/default Blazor styling
- xUnit tests

The application proves this workflow:

**Cognito Sign-In → Estimate → Approve → Create Job/Budget → Purchase Order → Issue PO → Post Actual Cost → Job Cost/Variance → SignalR refresh**

This is a POC. Favor simplicity, correctness, testability, and a complete runnable vertical slice over enterprise abstraction.

---


Also read `docs/09-local-verification-checklist.md` and use it for final local verification.
# 1. READ BEFORE CODING

Read these files completely before modifying or creating application code:

1. `README.md`
2. `docs/01-product-spec.md`
3. `docs/02-architecture.md`
4. `docs/03-use-cases.md`
5. `docs/04-ui-spec.md`
6. `docs/05-security-cognito.md`
7. `docs/07-test-plan.md`
8. `database/001_schema.sql`
9. `database/002_seed.sql`
10. `.github/copilot-instructions.md`
11. `docs/08-demo-data.md`

This file overrides `docs/06-agent-implementation-plan.md` only with respect to execution strategy: **implement all milestones in this single session rather than stopping after each milestone.**

Do not ask for confirmation between phases. Continue until the complete POC is implemented, built, and tested, or until a genuine external dependency prevents further progress.

---

# 2. SCOPE GUARDRAIL

Implement only:
- Cognito authentication
- Dashboard
- Cost Codes
- Vendors
- Estimates and Estimate Lines
- Estimate approval
- Jobs
- Estimate-to-Job Budget snapshot
- Purchase Orders and lines
- PO Issue/Close
- Actual Job Costs
- Job Cost summary
- SignalR job-cost refresh
- Validation
- Concurrency handling
- Automated tests
- Seed/demo data support

Do NOT add:
- microservices
- separate Web API
- CQRS/MediatR
- message broker
- Redis
- generic repository/unit-of-work abstractions
- event sourcing
- CRM
- scheduling
- AP/GL
- invoices/payments
- customer contracts
- selections
- change orders
- warranty
- document management
- multi-tenancy
- forecasting/EAC
- paid UI component dependencies

---

# 3. SOLUTION STRUCTURE

Create:

```text
BuilderPOC.sln
src/
  BuilderPOC.Web/
    Components/
      Layout/
      Pages/
        Dashboard/
        Estimates/
        Jobs/
        PurchaseOrders/
        Setup/
    Data/
      ISqliteConnectionFactory.cs
      SqliteConnectionFactory.cs
      DatabaseInitializer.cs
      DemoDataSeeder.cs
      Repositories/
    Models/
    Services/
    Hubs/
    Security/
    Program.cs
    appsettings.json
    appsettings.Development.json
tests/
  BuilderPOC.Tests/
database/
  001_schema.sql
  002_seed.sql
```

One web project plus one automated-test project is enough.

Do not split Domain/Application/Infrastructure into separate projects for this POC.

---

# 4. DATABASE — DAPPER + SQLITE

Implement `database/001_schema.sql` using SQLite and `database/002_seed.sql` for master data.

Required packages:
- `Dapper`
- `Microsoft.Data.Sqlite`

Do NOT add Entity Framework Core or any ORM.

Implement `ISqliteConnectionFactory`/`SqliteConnectionFactory` and a `DatabaseInitializer`. Default Development connection string: `Data Source=builderpoc.db;Foreign Keys=True`. The initializer must create/upgrade the local database from versioned SQL scripts and be safe to run repeatedly.

Use explicit SQL and focused data-access/repository classes. Do not use a generic repository. All SQL must be parameterized. Use `CommandDefinition` and cancellation tokens where practical.

Financial values are .NET `decimal`. UTC timestamps are stored as ISO-8601 UTC text. Enable foreign keys.

SQLite does not support stored procedures or SQL Server `rowversion`. Implement optimistic concurrency using integer `Version` columns on Estimate, Job, and PurchaseOrder. Updates use `WHERE ... Version=@ExpectedVersion` and increment Version. Zero affected rows means concurrency conflict.

Keep SQL isolated so the future production path can move to SQL Server + Dapper + stored procedures without changing UI/service contracts.

# 5. AUTHENTICATION


## Development authentication — Cognito must NOT block the POC
AWS Cognito may not be configured yet. The POC must work completely without AWS.

Support two authentication modes:

### `Development`
When BOTH conditions are true:
- ASP.NET Core environment is `Development`; and
- `Authentication:Mode` is `Development`;

use a local development authentication handler that creates an authenticated principal with:
- `sub = local-dev-user`
- `email = developer@builderpoc.local`
- `name = POC Developer`

### `Cognito`
Use Cookie + OpenID Connect authentication as described below.

Mandatory safety rule:
- If `Authentication:Mode=Development` while the ASP.NET Core environment is not Development, fail application startup with a clear configuration exception.
- Never silently enable development authentication in Production.

Automated tests must use synthetic claims/test current-user implementations and must never call AWS.


### AWS Cognito mode

Configure ASP.NET Core authentication using:
- Cookie authentication for application session.
- OpenID Connect challenge against AWS Cognito.
- Authorization Code flow.
- scopes: `openid`, `email`, `profile`.
- authenticated-user fallback authorization policy.

Configuration must be external:

```json
{
  "Authentication": {
    "Cognito": {
      "Authority": "",
      "ClientId": "",
      "ClientSecret": "",
      "MetadataAddress": "",
      "SignedOutRedirectUri": "/"
    }
  }
}
```

Do not put real values in source.

Implement `ICurrentUserService`.

Use Cognito `sub` as the persistent user identity for audit fields.

If real Cognito configuration is absent in local/test execution:
- application code must still compile;
- automated tests must not require live Cognito;
- tests should use synthetic authenticated ClaimsPrincipal instances;
- document the required Cognito configuration in README.

Do not weaken production authentication merely to make local testing easier.

---

# 6. APPLICATION SERVICES

At minimum implement:

```text
ICostCodeService
IVendorService
IEstimateService
IJobService
IPurchaseOrderService
IJobCostService
ICurrentUserService
```

Razor components call services.

Razor components MUST NOT open SQLite connections or execute Dapper/SQL directly.

Business rules belong in services.

Use async Dapper APIs, `CommandDefinition`, and CancellationToken where practical.

---

# 7. ESTIMATING

Implement:
- Estimate list
- New Estimate
- Edit Draft Estimate
- Add/remove lines
- calculated line amount
- calculated total
- Approve Estimate
- Approved Estimate read-only

Rules:
- ProjectName required.
- EstimateNumber unique.
- At least one line before approval.
- Quantity > 0.
- UnitCost >= 0.
- Approved estimate cannot be edited.

Calculation:

```text
EstimateLineAmount = Quantity * UnitCost
EstimateTotal = SUM(EstimateLineAmount)
```

---

# 8. JOB / BUDGET

An Approved Estimate can create exactly one Job.

Create Job and JobBudgetLines in one transaction.

For every EstimateLine:

```text
JobBudgetLine.BudgetAmount =
    EstimateLine.Quantity * EstimateLine.UnitCost
```

The budget is a snapshot.

Never calculate the live Job budget by querying EstimateLine after Job creation.

Job statuses:
- Active
- Closed

Closed Job rejects new PO/cost activity.

---

# 9. PURCHASE ORDERS

Implement:
- PO list
- create/edit Draft
- add/remove lines
- Issue
- Close
- read-only after Issue

Statuses:
- Draft
- Issued
- Closed

Rules:
- unique PONumber
- active Job required
- active Vendor
- at least one line before Issue
- line Amount > 0
- Draft does NOT contribute to committed
- Issued and Closed DO contribute to committed

---

# 10. ACTUAL JOB COST

Implement Job > Actual Costs.

Fields:
- TransactionDate
- CostCode
- Vendor optional
- PurchaseOrder optional
- ReferenceNumber
- Description
- Amount

Rules:
- Active Job.
- Amount > 0.
- If PurchaseOrderId supplied, PO must belong to same Job.
- Normal POC UI is append-only; do not add delete/edit transaction features.

---

# 11. JOB COST CALCULATION

This is the most important screen.

Return every CostCode appearing in any of:
- JobBudgetLine
- qualifying PurchaseOrderLine
- JobCostTransaction

For each CostCode:

```text
Budget =
SUM(JobBudgetLine.BudgetAmount)

Committed =
SUM(PurchaseOrderLine.Amount)
WHERE PurchaseOrder.Status IN ('Issued','Closed')

Actual =
SUM(JobCostTransaction.Amount)

Variance =
Budget - Committed
```

Job totals are sums of detail.

Implement DTOs similar to:

```csharp
public sealed record JobCostLineDto(
    int CostCodeId,
    string CostCode,
    string Description,
    decimal Budget,
    decimal Committed,
    decimal Actual,
    decimal Variance);

public sealed record JobCostSummaryDto(
    int JobId,
    string JobNumber,
    string JobName,
    IReadOnlyList<JobCostLineDto> Lines,
    decimal Budget,
    decimal Committed,
    decimal Actual,
    decimal Variance);
```

Do not duplicate the authoritative financial calculation in Razor/JavaScript.

---

# 12. SIGNALR

Add an explicit `JobCostHub`.

Group:
```text
job:{jobId}
```

Event:
```text
JobCostUpdated
```

Payload:
```json
{ "jobId": 123 }
```

Send the event after successful committed transactions that change job-cost information, especially:
- PO Issue
- PO Close
- Actual Cost Post

The Job Cost page listens for the event and re-queries `IJobCostService`.

Do not send authoritative financial totals through SignalR.

---

# 13. UI

Implement the mockups in `docs/04-ui-spec.md`.

Navigation:
- Dashboard
- Estimates
- Jobs
- Purchase Orders
- Setup
  - Cost Codes
  - Vendors

Job Detail tabs:
- Overview
- Budget
- Purchase Orders
- Actual Costs
- Job Cost

Use Bootstrap/default Blazor components.

UI requirements:
- desktop-first but reasonably responsive
- currency right aligned
- status badges
- inline validation
- confirmation for Approve, Issue, Close
- buttons disabled while command executing
- concise success/error feedback
- meaningful empty states
- loading indicators where appropriate

Do not spend excessive time on visual polish before functionality/tests pass.

---

# 14. ERROR AND CONCURRENCY HANDLING

Handle expected validation failures cleanly.

Handle `DbUpdateConcurrencyException` for Estimate, Job, PO.

User-facing message:

> This record was changed by another user. Reload and try again.

Unexpected errors:
- log server-side;
- display generic error;
- do not expose stack trace, SQL, secrets, tokens, or connection information.

---

# 15. AUTOMATED TESTS — REQUIRED, NOT OPTIONAL

Implement tests during development, not after everything else.

At minimum include tests for:

## Estimate
- line amount calculation
- total calculation
- cannot approve empty estimate
- approve valid estimate
- cannot edit approved estimate

## Job
- cannot create Job from Draft estimate
- Approved Estimate creates Job
- Budget snapshot equals Estimate line amounts
- cannot create second Job from same Estimate
- Closed Job blocks new cost activity

## PO
- Draft PO excluded from committed
- Issued PO included in committed
- Closed PO remains included
- cannot issue empty PO
- cannot modify issued PO
- cannot create PO for Closed Job

## Actual Cost
- positive cost posts successfully
- zero/negative rejected
- PO from another Job rejected
- Closed Job rejected

## Job Cost
Test CostCode represented as:
- budget only
- committed only
- actual only
- all three

Verify:
- Budget
- Committed
- Actual
- Variance
- job totals

## Authentication
- CurrentUserService reads Cognito `sub`
- email/display claims
- unauthenticated state handled appropriately

## Concurrency
Test concurrency behavior where practical.

Use real SQLite integration tests (temporary database files or an in-memory SQLite connection kept open for the test). Do not mock Dapper and do not require SQL Server.

No test may call live Cognito.

---

# 16. BUILD / TEST LOOP

Work autonomously using this loop:

```text
Implement small vertical slice
        ↓
Build
        ↓
Run relevant tests
        ↓
Fix compilation/test failures
        ↓
Continue
```

At the end run:

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

If formatting tooling is available, also run:

```bash
dotnet format --verify-no-changes
```

If it fails because generated/new code needs formatting, format and rerun.

Do not declare success if build or required tests fail.

---

# 17. DEMO DATA / ACCEPTANCE TEST

Ensure the application can demonstrate:

```text
Estimate EST-1001
  Foundation  $35,000
  Framing     $72,000
              --------
  Total      $107,000

Approve
   ↓
JOB-1001
Budget       $107,000

PO-1001
Framing       $68,000
Issue
   ↓
Committed     $68,000

Actual Cost
Framing       $64,500
   ↓

JOB COST
Budget       $107,000
Committed     $68,000
Actual        $64,500
Variance      $39,000

Framing:
Budget         $72,000
Committed      $68,000
Actual         $64,500
Variance        $4,000
```

Open the Job Cost page in two sessions. A PO Issue/Close or Actual Cost Post from one session must cause the other session viewing that same job to refresh without a full browser reload.

---


# 18. REALISTIC DEMO DATA — REQUIRED

After SQLite schema initialization, populate realistic fictional construction data according to `docs/08-demo-data.md`.

Implement a `DemoDataSeeder` (or equivalent) with these requirements:
- Dapper/SQLite based.
- idempotent.
- enabled automatically in Development through `DemoData:Enabled=true`.
- disabled by default in Production.
- never duplicates records.
- uses fictional data only.
- uses `demo-seed-user` for seed audit fields.
- creates Approved and Draft Estimates.
- creates Active and Closed Jobs.
- creates Draft, Issued, and Closed POs.
- creates Actual Job Cost transactions.
- creates at least one negative cost-code variance so the Job Cost UI visibly demonstrates an over-budget commitment.
- uses relative dates so demo data does not look stale.

After seeding, the first application launch should already have enough data to demonstrate Dashboard, Estimates, Jobs, Purchasing, Actual Costs, and Job Cost without manual data entry.

Add automated verification tests for the demo seeder and expected demo totals.

The user must still be able to create new records manually after seeding.


# 19. DOCUMENTATION TO PRODUCE

Update/create README with:
- prerequisites
- SQLite database initialization
- database initialization/setup
- Cognito User Pool/app-client configuration
- callback/sign-out URL placeholders
- local secret configuration
- run instructions
- test instructions
- demo workflow

Create `IMPLEMENTATION_NOTES.md` containing:
- completed scope
- any deviations
- assumptions
- external configuration still required
- known POC limitations

Do not leave essential setup knowledge only in agent output/chat.

---

# 20. FINAL SELF-REVIEW

Before finishing, verify:

- [ ] solution builds
- [ ] tests pass
- [ ] no secrets committed
- [ ] no business page bypasses authentication
- [ ] Razor components do not execute SQL or open database connections directly
- [ ] Approved Estimate immutable
- [ ] Budget snapshot implemented
- [ ] Draft PO excluded from committed
- [ ] Issued/Closed PO included
- [ ] Actual cost rules enforced server-side
- [ ] Job Cost calculations correct
- [ ] SignalR refresh implemented
- [ ] concurrency handled
- [ ] realistic demo data seeded in Development
- [ ] demo seeder idempotency tested
- [ ] README updated
- [ ] IMPLEMENTATION_NOTES.md created

Search the repository for:
- TODO
- FIXME
- NotImplementedException
- hard-coded passwords/secrets
- temporary authentication bypasses

Resolve anything that prevents the POC from meeting the specification.

---

# 21. FINAL RESPONSE FORMAT

When implementation is complete, respond with:
1. concise implementation summary;
2. build result;
3. test result with passed/failed count;
4. database setup instructions;
5. Cognito configuration still required;
6. how to run;
7. demo steps;
8. any known limitations.

Do not merely describe code that should be written. **Create the application, run it through the build/test loop, and leave the repository in a runnable state.**
