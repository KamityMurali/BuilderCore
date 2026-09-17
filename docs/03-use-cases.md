# Use Cases and Acceptance Criteria

## UC-01 Sign in
**Actor:** User  
**Precondition:** Cognito user exists.  
**Flow:** Open protected route → redirect to Cognito managed login → authenticate → redirect to app → authenticated session established.  
**Acceptance:** User sees application and email/name. Unauthenticated user cannot access business pages.

## UC-02 Maintain Cost Code
**Actor:** Authenticated User  
**Flow:** Open Setup > Cost Codes → Add/Edit → Save.  
**Validation:** Code required and unique; Description required.  
**Acceptance:** Saved cost code is selectable on estimate, PO, and cost forms.

## UC-03 Maintain Vendor
**Actor:** Authenticated User  
**Validation:** VendorName required; Email optional but valid when supplied.  
**Acceptance:** Active vendor appears in PO and job-cost vendor selectors.

## UC-04 Create Estimate
**Actor:** Authenticated User  
**Flow:** Create header → add lines → save.  
**Validation:** ProjectName required; each line requires CostCode, Quantity > 0, UnitCost >= 0.  
**Calculation:** LineAmount = Quantity × UnitCost; EstimateTotal = sum lines.  
**Acceptance:** Draft can be reopened and edited.

## UC-05 Approve Estimate
**Precondition:** Draft with >= 1 line.  
**Flow:** Click Approve → confirmation → service approves.  
**Acceptance:** Status becomes Approved; header and lines become read-only; Create Job becomes available.

## UC-06 Create Job
**Precondition:** Estimate Approved and no Job exists for it.  
**Flow:** Click Create Job → enter JobNumber/JobName → confirm.  
**Postcondition:** Job and budget snapshot are created atomically.  
**Acceptance:** Sum of JobBudgetLine equals estimate total at creation.

## UC-07 Create Purchase Order
**Precondition:** Active Job.  
**Flow:** New PO → choose Job/Vendor → add lines → Save Draft.  
**Validation:** unique PO number; line amount > 0.  
**Acceptance:** Draft PO does not affect committed cost.

## UC-08 Issue Purchase Order
**Precondition:** Draft PO with >= 1 line.  
**Flow:** Issue → confirmation.  
**Acceptance:** Status Issued; PO becomes read-only; lines now contribute to committed; job-cost viewers receive refresh notification.

## UC-09 Post Actual Cost
**Precondition:** Active Job.  
**Flow:** Job > Actual Costs > Add Cost → enter date, cost code, vendor optional, PO optional, reference, amount → Post.  
**Validation:** amount > 0; if PO supplied it must belong to same Job.  
**Acceptance:** transaction appears in history and Actual total updates; job-cost viewers refresh.

## UC-10 View Job Cost
**Output by Cost Code:**
- Budget
- Committed
- Actual
- Variance = Budget - Committed

**Job totals:** Sum columns.

**Acceptance:** Cost code appears if present in budget, issued/closed PO, or actual costs, even if absent from the other sources.

## UC-11 Close PO
Issued PO → Close. Closed remains included in committed for POC.

## UC-12 Close Job
Active Job → Close. No further POs or costs can be added.

# Negative tests
- Cannot approve empty estimate.
- Cannot edit approved estimate.
- Cannot create two jobs from one estimate.
- Cannot issue empty PO.
- Cannot create PO for closed job.
- Cannot post cost to closed job.
- Cannot post zero/negative actual cost.
- Draft PO does not affect committed.
- Unauthenticated user is redirected/challenged.
