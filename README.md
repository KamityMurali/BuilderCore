# BuilderPOC — Implementation-Ready Specification

A deliberately small construction estimating, purchasing, and job-cost POC inspired by the core workflow of residential construction ERP products.

## Technology
- .NET 10
- Blazor Server / Interactive Server
- SignalR
- Dapper
- Microsoft.Data.Sqlite
- SQLite
- AWS Cognito User Pool using OpenID Connect (OIDC)
- Bootstrap (default); no paid UI dependency required

## POC workflow
1. User signs in with AWS Cognito.
2. User maintains Cost Codes and Vendors.
3. User creates an Estimate and Estimate Lines.
4. User approves the Estimate.
5. User creates a Job from the approved Estimate.
6. Estimate lines are snapshotted into Job Budget Lines.
7. User creates and issues Purchase Orders.
8. User posts Actual Job Costs.
9. Job Cost screen shows Budget, Committed, Actual, and Variance.
10. SignalR refreshes the Job Cost view for users viewing the same job.

## Core financial definitions
- Budget = SUM(JobBudgetLine.BudgetAmount)
- Committed = SUM(PurchaseOrderLine.Amount) for Issued/Closed POs
- Actual = SUM(JobCostTransaction.Amount)
- Variance = Budget - Committed

For this POC, variance is intentionally commitment-based. A future version can add Forecast/Estimate-at-Completion.

## One-prompt agent build
For Cursor Agent, GitHub Copilot coding agent, or Codex, paste the complete contents of `ONE_PROMPT_BUILD.md` as the build instruction. The agent is instructed to read the remaining specification files itself, implement the entire POC, run automated tests, fix failures, and leave the repository runnable.

Supporting specifications:
1. `docs/01-product-spec.md`
2. `docs/02-architecture.md`
3. `docs/03-use-cases.md`
4. `docs/04-ui-spec.md`
5. `docs/05-security-cognito.md`
6. `docs/08-demo-data.md`
7. `docs/07-test-plan.md`
8. `database/001_schema.sql`
9. `database/002_seed.sql`
10. `.github/copilot-instructions.md`

## Agent completion definition
The POC is complete when a developer can:
- authenticate through Cognito;
- create and approve an estimate;
- convert it to a job;
- create and issue a PO;
- post an actual cost;
- see correct job-cost totals;
- open the same job in two sessions and see a financial refresh after a cost-changing action;
- run automated tests successfully.

## Persistence decision
This version intentionally uses **Dapper + SQLite**, not Entity Framework. SQLite requires no separate database server and makes the POC zero-setup. SQLite has no stored procedures; a later production migration can use Dapper + SQL Server stored procedures behind the same service/data-access boundaries.

Development startup initializes `builderpoc.db`, seeds master data, and (when enabled) seeds realistic fictional demo data.
