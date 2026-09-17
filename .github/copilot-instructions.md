# BuilderPOC Coding Instructions

## Stack
- .NET 10
- C#
- Blazor Interactive Server
- Dapper + SQLite
- SignalR
- AWS Cognito OIDC

## Mandatory rules
- POC modular monolith; no microservices.
- Razor components never open SQLite connections or execute SQL.
- All domain commands go through services.
- Use async/await and CancellationToken for I/O.
- Use decimal for money.
- UTC for persisted audit timestamps.
- Use Cognito `sub` for audit user identity.
- Never store passwords or tokens in application DB.
- Never commit secrets.
- Approved Estimate is immutable.
- Job budget is a snapshot, not a live Estimate query.
- Draft PO is excluded from committed cost.
- Issued/Closed PO is included in committed cost.
- Variance for POC = Budget - Committed.
- SignalR events invalidate/reload data; event payloads are not authoritative financial state.
- Server-side validation is mandatory.
- Add tests for every business rule changed.

## Scope guardrail
Do not add CRM, scheduling, accounting, invoices, payments, change orders, bidding, warranty, documents, mobile, multi-tenancy, CQRS/MediatR, message brokers, or forecasting unless explicitly requested.

## POC local execution
- AWS Cognito is optional during initial POC development.
- Development auth is allowed only when environment=Development AND Authentication:Mode=Development.
- Fail startup if Development auth is configured outside Development.
- SQLite is the required zero-setup POC database. Use Dapper + Microsoft.Data.Sqlite; do not add EF Core.
- SQLite has no stored procedures; use parameterized SQL in focused data-access classes.
- Use integer Version columns for optimistic concurrency.
- Seed realistic fictional demo data in Development according to `docs/08-demo-data.md`.
- Demo seeding must be idempotent and covered by tests.
