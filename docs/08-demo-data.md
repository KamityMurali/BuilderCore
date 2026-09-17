# Demo Data Specification

## Purpose
The POC must start with realistic-looking construction data so the application can be demonstrated immediately without manually entering every record.

All names, addresses, vendors, references, and amounts below are fictional demo data. Do not use real customer PII.

## Seeding behavior
- Seed demo data only in Development or when `DemoData:Enabled=true`.
- Seeding must be idempotent.
- Never duplicate demo records on restart.
- Production must default `DemoData:Enabled=false`.
- Use a `DemoDataSeeder` that calls the same application/data-access layer or parameterized Dapper commands; it must be transactional and idempotent.
- Seed after schema initialization/database creation.
- Keep master-data seed separate from transactional demo-data seed.

Example configuration:

```json
{
  "Authentication": {
    "Mode": "Development"
  },
  "DemoData": {
    "Enabled": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=buildercore.db"
  }
}
```

## Cost Codes
| Code | Description |
|---|---|
| 1000 | Site Work |
| 2000 | Foundation |
| 3000 | Framing |
| 4000 | Roofing |
| 5000 | Plumbing |
| 6000 | Electrical |
| 7000 | HVAC |
| 8000 | Flooring |
| 9000 | Painting |

## Vendors
Use fictional companies:
- Summit Concrete Works
- Redwood Framing Co.
- Pacific Crest Roofing
- ClearFlow Plumbing
- BrightLine Electric
- Golden State HVAC
- Heritage Flooring
- WestBay Painting

Use `.example` email domains where email is needed.

## Demo Jobs

### Demo 1 — Active job with healthy commitment variance
Estimate: `EST-1001`
Project: `1847 Willow Creek Drive`
Status: Approved

Estimate/Budget:
| Cost Code | Description | Budget |
|---|---|---:|
| 1000 | Site Work | 18,500 |
| 2000 | Foundation | 42,000 |
| 3000 | Framing | 78,500 |
| 4000 | Roofing | 24,000 |
| 5000 | Plumbing | 31,500 |
| 6000 | Electrical | 29,000 |
| 7000 | HVAC | 26,500 |
| 8000 | Flooring | 22,000 |
| 9000 | Painting | 18,000 |
| | **Total** | **290,000** |

Job: `JOB-1001`
Job Name: `1847 Willow Creek Drive`
Status: Active

Purchase Orders:
- `PO-1001` Summit Concrete Works — Foundation — $40,500 — Issued
- `PO-1002` Redwood Framing Co. — Framing — $75,000 — Issued
- `PO-1003` Pacific Crest Roofing — Roofing — $23,500 — Issued
- `PO-1004` ClearFlow Plumbing — Plumbing — $30,800 — Issued
- `PO-1005` BrightLine Electric — Electrical — $27,900 — Draft

Actual costs:
- Foundation / Summit Concrete Works / `INV-24081` / $40,500
- Framing / Redwood Framing Co. / `INV-11842` / $52,000
- Roofing / Pacific Crest Roofing / `INV-77301` / $11,750

Expected committed total = $169,800 because Draft PO-1005 is excluded.
Expected actual total = $104,250.
Expected commitment variance = $120,200.

### Demo 2 — Active job with a cost-code over-commitment
Estimate: `EST-1002`
Project: `932 Harbor View Lane`
Status: Approved

Budget:
- Site Work $21,000
- Foundation $45,000
- Framing $82,000
- Roofing $25,500
- Plumbing $34,000
- Electrical $30,000
- HVAC $28,500
- Flooring $26,000
- Painting $19,500
Total = $311,500

Job: `JOB-1002`
Status: Active

Create issued POs so at least one cost code is visibly over budget:
- Framing PO = $86,500 against $82,000 budget.
- Electrical PO = $29,200 against $30,000 budget.
- HVAC PO = $27,900 against $28,500 budget.

Post several partial actual costs.

This job demonstrates a negative variance on Framing.

### Demo 3 — Draft estimate
Estimate: `EST-1003`
Project: `4172 Stonebridge Court`
Status: Draft

Include 4–6 estimate lines but do not create a Job.
This demonstrates editable estimate workflow.

### Demo 4 — Closed job
Estimate: `EST-1004`
Project: `675 Meadow Ridge Avenue`
Status: Approved

Create `JOB-1004` with Status Closed.
Include budget, several Closed POs, and actual transactions.
This demonstrates read-only/closed-job behavior.

## Dashboard expectations
After demo seed, Dashboard should immediately show useful information:
- at least 3 Jobs;
- at least 1 Draft Estimate;
- active and closed Jobs;
- Draft and Issued/Closed POs;
- actual transactions;
- one cost code with negative variance.

## Dates
Generate dates relative to the seed execution date rather than hard-coding stale dates:
- estimates: 30–60 days ago;
- active-job POs: 5–30 days ago;
- actual transactions: 1–20 days ago.
Use UTC for audit timestamps.

## Demo identity
For seeded audit fields use:
`demo-seed-user`

Do not impersonate the currently logged-in user for seed records.

## Verification tests
Add automated tests that verify:
1. Seeder is idempotent.
2. EST-1001/JOB-1001 totals match expected values.
3. Draft PO is excluded from committed.
4. JOB-1002 has at least one negative cost-code variance.
5. EST-1003 remains Draft and has no Job.
6. JOB-1004 is Closed.
