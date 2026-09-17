# UI Specification and Mockups

## Global layout

```text
+------------------------------------------------------------------+
| BuilderCore                                      user@example.com  |
+----------------+-------------------------------------------------+
| Dashboard      |                                                 |
| Estimates      |              PAGE CONTENT                       |
| Jobs           |                                                 |
| Purchase Orders|                                                 |
| Setup          |                                                 |
|  Cost Codes    |                                                 |
|  Vendors       |                                                 |
+----------------+-------------------------------------------------+
```

Responsive desktop-first POC. Use Bootstrap components. Grids may be simple HTML/Bootstrap tables.

## Dashboard

```text
+------------------------------------------------------------------+
| Dashboard                                                        |
|                                                                  |
| +---------------+ +---------------+ +---------------+            |
| | Active Jobs   | | Draft Est.    | | Open POs      |            |
| |      4        | |      2        | |      8        |            |
| +---------------+ +---------------+ +---------------+            |
|                                                                  |
| Recent Jobs                                                      |
| ---------------------------------------------------------------  |
| Job       Name              Budget      Committed      Actual     |
| JOB-1001  123 Main St       $107,000     $68,000       $64,500    |
+------------------------------------------------------------------+
```

## Estimate List

```text
Estimates                                           [ + New Estimate ]

Search [________________]

Estimate    Project                 Status      Total       Actions
EST-1001    123 Main Street        Approved    $107,000    View
EST-1002    25 Oak Avenue          Draft        $85,000    Edit
```

## Estimate Edit

```text
Estimate EST-1002                         Status: DRAFT

Project Name * [25 Oak Avenue________________________]
Description    [_____________________________________]

Lines
+---------------------------------------------------------------+
| Cost Code | Description | Qty | Unit Cost | Amount |          |
| 2000      | Foundation  | 1   | 35,000    | 35,000 | Remove   |
| 3000      | Framing     | 1   | 50,000    | 50,000 | Remove   |
+---------------------------------------------------------------+
                                         Total: $85,000

[+ Add Line]                           [Save] [Approve Estimate]
```

Approval requires confirmation:
`Approve EST-1002? Approved estimates cannot be edited.`

## Approved Estimate

```text
Estimate EST-1001                     Status: APPROVED

Project: 123 Main Street
Total:   $107,000

[Create Job]
```

All editable controls disabled/read-only.

## Create Job Modal

```text
Create Job from EST-1001

Job Number * [JOB-1001________]
Job Name   * [123 Main Street________________]

Budget to create: $107,000

[Cancel] [Create Job]
```

## Job Detail

```text
JOB-1001 | 123 Main Street                         ACTIVE

[Overview] [Budget] [Purchase Orders] [Actual Costs] [Job Cost]

Overview
Estimate:       EST-1001
Budget:         $107,000
Committed:       $68,000
Actual:          $64,500
Variance:        $39,000

[Close Job]
```

## Budget tab

```text
Cost Code     Description        Budget
2000          Foundation         $35,000
3000          Framing            $72,000
                              -----------
Total                           $107,000
```

Read-only in POC.

## Purchase Order Edit

```text
PO-1001                                        DRAFT

Job *       [JOB-1001 - 123 Main St v]
Vendor *    [Bay Area Framing v]

Lines
Cost Code       Description              Amount
[3000 v]        [Framing____________]    [68,000.00]

[+ Add Line]

Total: $68,000

[Save Draft] [Issue PO]
```

## Add Actual Cost

```text
Add Actual Cost

Job:          JOB-1001
Date *        [09/16/2026]
Cost Code *   [3000 - Framing v]
Vendor        [Bay Area Framing v]
PO            [PO-1001 v]
Reference     [INV-88291]
Description   [Framing progress billing____________]
Amount *      [$64,500.00]

[Cancel] [Post Cost]
```

## Job Cost — primary POC screen

```text
JOB-1001 / Job Cost
123 Main Street

+-------------+-------------+-------------+-------------+
| Budget      | Committed   | Actual      | Variance    |
| $107,000    | $68,000     | $64,500     | $39,000     |
+-------------+-------------+-------------+-------------+

Cost Code    Description     Budget     Committed   Actual    Variance
2000         Foundation      35,000          0          0       35,000
3000         Framing         72,000     68,000     64,500        4,000
---------------------------------------------------------------------
TOTAL                       107,000     68,000     64,500       39,000

Last refreshed: 10:42:15 PM
```

On `JobCostUpdated`, refresh cards/grid and update Last refreshed.

## UX rules
- Currency right-aligned.
- Status shown as badge.
- Destructive/state-transition actions require confirmation.
- Disable buttons while async command is running.
- Show field validation inline.
- Show one concise success/error notification after commands.
- Empty states explain the next action.
