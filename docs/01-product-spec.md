# Product Specification

## 1. Product goal
Prove a simple end-to-end construction cost-control workflow:

**Estimate → Approved Estimate → Job/Budget → Purchase Order → Actual Cost → Job Cost**

This is a POC, not a full ERP.

## 2. Personas
### Estimator
Creates and approves estimates.

### Purchasing User
Creates and issues purchase orders.

### Project/Cost User
Creates jobs from approved estimates, posts actual costs, and reviews job cost.

For the POC, any authenticated user may perform all actions. Authorization policies should still be isolated so roles can be introduced later.

## 3. In scope
- Cognito SSO
- Cost Codes
- Vendors
- Estimates
- Estimate Lines
- Estimate Approval
- Job creation from approved estimate
- Budget snapshot
- Purchase Orders and lines
- PO Issue/Close
- Actual Job Cost entry
- Job Cost summary and cost-code detail
- Real-time Job Cost refresh
- Basic audit fields

## 4. Out of scope
- CRM
- customer contracts
- selections/options
- scheduling
- AP/GL/accounting
- invoices and payments
- bidding
- change orders
- warranty
- document management
- mobile app
- multi-company/multi-tenant support
- complex RBAC
- hierarchical cost codes
- estimate revisions
- PO change orders
- forecasting/EAC
- direct AWS-resource credentials for end users

## 5. Business rules

### BR-001 Estimate states
Allowed: `Draft`, `Approved`.

### BR-002 Approved estimate
An Approved estimate and its lines are read-only.

### BR-003 Create Job
A Job can be created only from an Approved estimate.

### BR-004 One Job per Estimate
For the POC, one estimate can create at most one job.

### BR-005 Budget snapshot
Job creation copies each estimate line's calculated amount into a JobBudgetLine. Subsequent queries do not derive the job budget from EstimateLine.

### BR-006 Job states
Allowed: `Active`, `Closed`.

### BR-007 Closed Job
No new PO or Job Cost transaction may be added to a Closed job.

### BR-008 PO states
Allowed: `Draft`, `Issued`, `Closed`.

### BR-009 PO edit
PO header/lines can be edited only while Draft.

### BR-010 Commitment
Only `Issued` and `Closed` PO lines contribute to Committed.

### BR-011 Actual cost
Job Cost transactions are append-only in the normal UI for the POC. No delete UI.

### BR-012 Financial precision
Currency: `decimal(18,2)`. Estimate quantities/unit cost: `decimal(18,2)` for POC.

### BR-013 UTC audit time
Persist audit timestamps in UTC.

### BR-014 User identity
Persist Cognito `sub` as CreatedBy/UpdatedBy. Display email/name from claims where available.

## 6. Acceptance scenario
Given cost codes and vendors exist:
1. Create EST-1001 with Foundation $35,000 and Framing $72,000.
2. Total = $107,000.
3. Approve estimate.
4. Create JOB-1001.
5. Job budget = $107,000.
6. Create PO-1001, Framing = $68,000, then Issue.
7. Committed = $68,000.
8. Post actual Framing cost = $64,500.
9. Actual = $64,500.
10. Variance = $39,000 at job total (`107,000 - 68,000`).
11. Framing variance = $4,000 (`72,000 - 68,000`).
