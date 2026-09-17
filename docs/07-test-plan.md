# Test Plan — Dapper + SQLite

Use real SQLite databases for integration tests. Prefer a unique temporary `.db` file per test fixture, or an in-memory SQLite connection kept open for the entire test. Do not mock Dapper itself.

## Unit tests
- Estimate line amount and total.
- Job-cost variance.
- Current user maps Entra/local `sub`.

## Integration/service tests
- Schema initializer creates required tables and can run repeatedly safely.
- Estimate: Draft creation; approve with lines; reject empty approval; reject edit after approval.
- Job: reject Draft estimate; create from Approved; exact budget snapshot; reject second Job.
- PO: Draft excluded from committed; Issued included; Closed remains included; reject empty issue; reject mutation after issue; reject Closed Job.
- Actual: positive posts; <=0 rejected; Closed Job rejected; PO from another Job rejected.
- Job Cost: budget-only, committed-only, actual-only, and combined cost codes; totals equal detail; Draft POs excluded.
- Concurrency: stale Estimate and PO `Version` updates affect zero rows and return concurrency conflict.
- Demo seeder: idempotent and expected scenarios/totals are present.
- Authentication: synthetic ClaimsPrincipal only; no test calls Azure Entra ID.

## Manual SignalR test
Two browser sessions view the same job. Issue/close a PO or post actual cost in one; the other re-queries and refreshes without a full browser reload. A browser viewing another job should not refresh unnecessarily.

## Definition of done
`dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build` all succeed; no secrets are committed; the Product Spec acceptance scenario passes.
