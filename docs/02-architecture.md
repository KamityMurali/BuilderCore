# Architecture Specification — Dapper + SQLite

## 1. Style
Use a modular monolith: .NET 10 Blazor Web App (Interactive Server), application services, focused Dapper data-access classes, SQLite, SignalR, and switchable Development/Cognito authentication.

```text
Browser -> Blazor Server -> Application Services -> Dapper -> Microsoft.Data.Sqlite -> builderpoc.db
                              |                    \-> JobCostHub
                              \-> ICurrentUserService -> Local Dev Auth / Cognito OIDC
```

## 2. Structure
```text
src/BuilderPOC.Web/
  Components/
  Data/
    ISqliteConnectionFactory.cs
    SqliteConnectionFactory.cs
    DatabaseInitializer.cs
    DemoDataSeeder.cs
    Repositories/
      CostCodeRepository.cs
      VendorRepository.cs
      EstimateRepository.cs
      JobRepository.cs
      PurchaseOrderRepository.cs
      JobCostRepository.cs
  Models/
  Services/
  Hubs/
  Security/
  Program.cs
```

## 3. Data-access rules
- Use `Dapper` and `Microsoft.Data.Sqlite`; do not add EF Core.
- All SQL parameters must be parameterized. Never concatenate user input into SQL.
- Razor components never open connections or execute SQL.
- Services own business rules and transaction boundaries; repositories/data-access classes own SQL.
- Avoid generic `IRepository<T>` and Unit of Work abstractions.
- Open connections late and dispose promptly. Enable SQLite foreign keys for every connection.
- Use `CommandDefinition` with `CancellationToken` for Dapper operations where practical.
- Map query results into explicit DTO/model types.

## 4. Database initialization
`DatabaseInitializer` creates the database file if absent and applies versioned scripts from `database/` using a `SchemaVersion` table. Scripts must be idempotent or tracked so they execute once. In Development, initialize schema, master seed, then optional demo seed. Production never auto-enables demo data.

Default local connection string: `Data Source=builderpoc.db;Foreign Keys=True`.

## 5. Transactions
Use explicit `SqliteTransaction` for Estimate approval, Job creation/budget snapshot, PO issue/close, and actual-cost posting. SignalR notification occurs only after a successful commit.

## 6. Optimistic concurrency
SQLite has no SQL Server `rowversion`. Use application-managed integer `Version` on Estimate, Job, and PurchaseOrder. Updates must use `WHERE Id=@Id AND Version=@ExpectedVersion` and atomically set `Version=Version+1`. If affected rows = 0, return a concurrency conflict: `This record was changed by another user. Reload and try again.`

## 7. Job-cost query
Implement the aggregation explicitly in SQL. Include a cost code if it exists in budget, qualifying Issued/Closed PO lines, or actual transactions. `Committed` excludes Draft POs. Calculate `Variance = Budget - Committed`. Re-query after `JobCostUpdated`; do not trust totals in the SignalR event.

## 8. Future SQL Server migration
Keep SQL in focused repository classes so a future provider can replace SQLite. SQL Server stored procedures can later replace repository SQL without changing Blazor components or service contracts. Provider-specific schema/concurrency/decimal behavior must be reviewed during that migration.
